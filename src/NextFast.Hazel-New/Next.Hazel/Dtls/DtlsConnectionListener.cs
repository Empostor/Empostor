using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.ObjectPool;
using Next.Hazel.Crypto;
using Next.Hazel.Dtls.Handshake;
using Next.Hazel.Dtls.Handshake.Constant;
using Next.Hazel.Udp;
using Serilog;
using Random = Next.Hazel.Dtls.Handshake.Constant.Random;

namespace Next.Hazel.Dtls;

/// <summary>
///     Listens for new UDP-DTLS connections and creates UdpConnections for them.
/// </summary>
/// <inheritdoc />
public class DtlsConnectionListener : UdpConnectionListener
{
    private const int MaxDatagramSize = 1200;

    private const int MaxCertFragmentSizeV0 = 1200;

    // Min MTU - UDP+IP header - 1 (for good measure. :))
    private const int MaxCertFragmentSizeV1 = 576 - 32 - 1;
    private static readonly ILogger Logger = Log.ForContext<DtlsConnectionListener>();
    private static readonly TimeSpan CookieHmacRotationTimeout = TimeSpan.FromHours(1.0);

    // Private key component of certificate's public key
    private readonly List<ByteSpan> encodedCertificates = [];
    /*private ByteSpan encodedCertificate;*/

    private readonly ConcurrentDictionary<IPEndPoint, PeerData> existingPeers = new();
    private RSA certificatePrivateKey;

    private int connectionSerial_unsafe;

    // HMAC key to validate ClientHello cookie
    private HMAC currentCookieHmac;
    private uint encodedCertificatesTotalSize;
    private DateTime nextCookieHmacRotation;
    private HMAC previousCookieHmac;

    private RandomNumberGenerator random;

    /// <summary>
    ///     Create a new instance of the DTLS listener
    /// </summary>
    /// <param name="endPoint"></param>
    /// <param name="ipMode"></param>
    /// <param name="readerPool"></param>
    public DtlsConnectionListener(IPEndPoint endPoint, ObjectPool<MessageReader> readerPool,
        IPMode ipMode = IPMode.IPv4)
        : base(endPoint, readerPool, ipMode)
    {
        random = RandomNumberGenerator.Create();

        currentCookieHmac = CreateNewCookieHMAC();
        previousCookieHmac = CreateNewCookieHMAC();
        nextCookieHmacRotation = DateTime.UtcNow + CookieHmacRotationTimeout;
    }

    public int PeerCount => existingPeers.Count;

    internal async ValueTask SendData(ByteSpan span, IPEndPoint endPoint)
    {
        var array = span.ToArray();
        await base.SendData(array, array.Length, endPoint);
    }

    internal override async ValueTask SendData(byte[] bytes, int length, IPEndPoint remoteEndPoint)
    {
        var span = new ByteSpan(bytes);

        if (!existingPeers.TryGetValue(remoteEndPoint, out var peer))
        {
            Logger.Warning("Peer not found");
            // Drop messages if we don't know how to send them
            return;
        }

        await peer.Semaphore.WaitAsync();
        {
            // If we're negotiating a new epoch, queue data
            if (peer.Epoch == 0 || peer.NextEpoch.State != HandshakeState.ExpectingHello)
            {
                ByteSpan copyOfSpan = new byte[span.Length];
                span.CopyTo(copyOfSpan);

                peer.QueuedApplicationDataMessage.Add(copyOfSpan);
                return;
            }

            // Send any queued application data now
            for (int ii = 0, nn = peer.QueuedApplicationDataMessage.Count; ii != nn; ++ii)
            {
                var queuedSpan = peer.QueuedApplicationDataMessage[ii];

                var outgoingRecord = new Record
                {
                    ContentType = ContentType.ApplicationData,
                    Epoch = peer.Epoch,
                    SequenceNumber = peer.CurrentEpoch.NextOutgoingSequence,
                    Length = (ushort)peer.CurrentEpoch.RecordProtection.GetEncryptedSize(queuedSpan.Length)
                };
                ++peer.CurrentEpoch.NextOutgoingSequence;

                // Encode the record to wire format
                ByteSpan packet = new byte[Record.Size + outgoingRecord.Length];
                var writer = packet;
                outgoingRecord.Encode(writer);
                writer = writer[Record.Size..];
                queuedSpan.CopyTo(writer);

                // Protect the record
                peer.CurrentEpoch.RecordProtection.EncryptServerPlaintext(
                    packet.Slice(Record.Size, outgoingRecord.Length)
                    , packet.Slice(Record.Size, queuedSpan.Length)
                    , ref outgoingRecord);

                await SendData(packet, remoteEndPoint);
            }

            peer.QueuedApplicationDataMessage.Clear();

            {
                var outgoingRecord = new Record
                {
                    ContentType = ContentType.ApplicationData,
                    Epoch = peer.Epoch,
                    SequenceNumber = peer.CurrentEpoch.NextOutgoingSequence,
                    Length = (ushort)peer.CurrentEpoch.RecordProtection.GetEncryptedSize(span.Length)
                };
                ++peer.CurrentEpoch.NextOutgoingSequence;

                // Encode the record to wire format
                ByteSpan packet = new byte[Record.Size + outgoingRecord.Length];
                var writer = packet;
                outgoingRecord.Encode(writer);
                writer = writer[Record.Size..];
                span.CopyTo(writer);

                // Protect the record
                peer.CurrentEpoch.RecordProtection.EncryptServerPlaintext(
                    packet.Slice(Record.Size, outgoingRecord.Length)
                    , packet.Slice(Record.Size, span.Length)
                    , ref outgoingRecord
                );

                await SendData(packet, remoteEndPoint);
            }
        }
        peer.Semaphore.Release();
    }

    /// <inheritdoc />
    public override async ValueTask DisposeAsync()
    {
        await base.DisposeAsync();

        random?.Dispose();
        random = null;

        currentCookieHmac?.Dispose();
        previousCookieHmac?.Dispose();
        currentCookieHmac = null;
        previousCookieHmac = null;

        foreach (var pair in existingPeers) pair.Value.Dispose();

        existingPeers.Clear();
    }

    /// <summary>
    ///     Set the certificate key pair for the listener
    /// </summary>
    /// <param name="certificate">Certificate for the server</param>
    public void SetCertificate(X509Certificate2 certificate)
    {
        if (!certificate.HasPrivateKey)
            throw new ArgumentException("Certificate must have a private key attached", nameof(certificate));

        var privateKey = certificate.GetRSAPrivateKey();
        if (privateKey == null)
            throw new ArgumentException("Certificate must be signed by an RSA key", nameof(certificate));

        certificatePrivateKey?.Dispose();
        certificatePrivateKey = privateKey;

        // Pre-fragment the certificate data
        var certificateData = Certificate.Encode(certificate);
        encodedCertificatesTotalSize = (uint)certificateData.Length;

        // The first certificate data needs to leave room for
        //  * Record header
        //  * ServerHello header
        //  * ServerHello payload
        //  * Certificate header
        var padding = Record.Size + Handshake.Handshake.Size + ServerHello.MinSize + Handshake.Handshake.Size;
        encodedCertificates.Add(certificateData[..Math.Min(certificateData.Length, MaxDatagramSize - padding)]);
        certificateData = certificateData[Math.Min(certificateData.Length, MaxDatagramSize - padding)..];

        // Subsequent certificate data needs to leave room for
        //  * Record header
        //  * Certificate header
        padding = Record.Size + Handshake.Handshake.Size;
        while (certificateData.Length > 0)
        {
            encodedCertificates.Add(certificateData[..Math.Min(certificateData.Length, MaxDatagramSize - padding)]);
            certificateData = certificateData[Math.Min(certificateData.Length, MaxDatagramSize - padding)..];
        }
        /*encodedCertificate = Certificate.Encode(certificate);*/
    }

    /// <summary>
    ///     Handle an incoming datagram from the network.
    ///     This is primarily a wrapper around ProcessIncomingMessage
    ///     to ensure `reader.Recycle()` is always called
    /// </summary>
    protected override ValueTask ProcessData(UdpReceiveResult data)
    {
        var message = new ByteSpan(data.Buffer);
        return ProcessIncomingMessage(message, data.RemoteEndPoint);
    }

    /// <summary>
    ///     Handle an incoming datagram from the network
    /// </summary>
    private async ValueTask ProcessIncomingMessage(ByteSpan message, IPEndPoint peerAddress)
    {
        if (!existingPeers.TryGetValue(peerAddress, out var peer))
        {
            await HandleNonPeerRecord(message, peerAddress);
            return;
        }

        await peer.Semaphore.WaitAsync();
        {
            // Each incoming packet may contain multiple DTLS
            // records
            while (message.Length > 0)
            {
                if (!Record.Parse(out var record, peer.ProtocolVersion, message))
                {
                    Logger.Error($"Dropping malformed record from `{peerAddress}`");
                    return;
                }

                message = message[Record.Size..];

                if (message.Length < record.Length)
                {
                    Logger.Error(
                        $"Dropping malformed record from `{peerAddress}` Length({record.Length}) AvailableBytes({message.Length})");
                    return;
                }

                var recordPayload = message[..record.Length];
                message = message[record.Length..];

                // Early-out and drop ApplicationData records
                if (record.ContentType == ContentType.ApplicationData && !peer.CanHandleApplicationData)
                {
                    Logger.Information($"Dropping ApplicationData record from `{peerAddress}` Cannot process yet");
                    continue;
                }

                // Drop records from a different epoch
                if (record.Epoch != peer.Epoch)
                {
                    // Handle existing client negotiating a new connection
                    if (record is { Epoch: 0, ContentType: ContentType.Handshake })
                    {
                        var handshakePayload = recordPayload;

                        if (!Handshake.Handshake.Parse(out var handshake, recordPayload))
                        {
                            Logger.Error($"Dropping malformed re-negotiation Handshake from `{peerAddress}`");
                            continue;
                        }

                        handshakePayload = handshakePayload[Handshake.Handshake.Size..];

                        if (handshake.FragmentOffset != 0 || handshake.Length != handshake.FragmentLength)
                        {
                            Logger.Error($"Dropping fragmented re-negotiation Handshake from `{peerAddress}`");
                            continue;
                        }

                        if (handshake.MessageType != HandshakeType.ClientHello)
                        {
                            Logger.Error($"Dropping non-ClientHello re-negotiation Handshake from `{peerAddress}`");
                            continue;
                        }

                        if (handshakePayload.Length < handshake.Length)
                            Logger.Error(
                                $"Dropping malformed re-negotiation Handshake from `{peerAddress}`: Length({handshake.Length}) AvailableBytes({handshakePayload.Length})");

                        if (!await HandleClientHello(peer, peerAddress, record, handshake, recordPayload,
                                handshakePayload)) return;
                        continue;
                    }

                    Logger.Error(
                        $"Dropping bad-epoch record from `{peerAddress}` RecordEpoch({record.Epoch}) CurrentEpoch({peer.Epoch})");
                    continue;
                }

                // Prevent replay attacks by dropping records
                // we've already processed
                var windowIndex = (int)(peer.CurrentEpoch.NextExpectedSequence - record.SequenceNumber - 1);
                var windowMask = 1ul << windowIndex;
                if (record.SequenceNumber < peer.CurrentEpoch.NextExpectedSequence)
                {
                    if (windowIndex >= 64)
                    {
                        Logger.Information(
                            $"Dropping too-old record from `{peerAddress}` Sequence({record.SequenceNumber}) Expected({peer.CurrentEpoch.NextExpectedSequence})");
                        continue;
                    }

                    if ((peer.CurrentEpoch.PreviousSequenceWindowBitmask & windowMask) != 0)
                    {
                        Logger.Information($"Dropping duplicate record from `{peerAddress}`");
                        continue;
                    }
                }

                // Validate record authenticity
                var decryptedSize = peer.CurrentEpoch.RecordProtection.GetDecryptedSize(recordPayload.Length);
                if (decryptedSize < 0)
                {
                    Logger.Information(
                        $"Dropping malformed record: Length {recordPayload.Length} Decrypted length: {decryptedSize}");
                    continue;
                }

                var decryptedPayload = recordPayload.ReuseSpanIfPossible(decryptedSize);

                if (!peer.CurrentEpoch.RecordProtection.DecryptCiphertextFromClient(decryptedPayload, recordPayload,
                        ref record))
                {
                    Logger.Error($"Dropping non-authentic record from `{peerAddress}`");
                    return;
                }

                recordPayload = decryptedPayload;

                // Update our squence number bookeeping
                if (record.SequenceNumber >= peer.CurrentEpoch.NextExpectedSequence)
                {
                    var windowShift = (int)(record.SequenceNumber + 1 - peer.CurrentEpoch.NextExpectedSequence);
                    peer.CurrentEpoch.PreviousSequenceWindowBitmask <<= windowShift;
                    peer.CurrentEpoch.NextExpectedSequence = record.SequenceNumber + 1;
                }
                else
                {
                    peer.CurrentEpoch.PreviousSequenceWindowBitmask |= windowMask;
                }

                switch (record.ContentType)
                {
                    case ContentType.ChangeCipherSpec:
                        if (peer.NextEpoch.State != HandshakeState.ExpectingChangeCipherSpec)
                        {
                            Logger.Error(
                                $"Dropping unexpected ChangeChiperSpec record from `{peerAddress}` State({peer.NextEpoch.State})");
                            break;
                        }

                        if (peer.NextEpoch.RecordProtection == null)
                        {
                            Debug.Assert(false,
                                "How did we receive a ChangeCipherSpec message without a pending record protection instance?");
                        }

                        if (!ChangeCipherSpec.Parse(recordPayload))
                        {
                            Logger.Error($"Dropping malformed ChangeCipherSpec message from `{peerAddress}`");
                            break;
                        }

                        // Migrate to the next epoch
                        peer.Epoch = peer.NextEpoch.Epoch;
                        peer.CanHandleApplicationData = false; // Need a Finished message
                        peer.CurrentEpoch.NextOutgoingSequenceForPreviousEpoch = peer.CurrentEpoch.NextOutgoingSequence;
                        peer.CurrentEpoch.PreviousRecordProtection?.Dispose();
                        peer.CurrentEpoch.PreviousRecordProtection = peer.CurrentEpoch.RecordProtection;
                        peer.CurrentEpoch.RecordProtection = peer.NextEpoch.RecordProtection;
                        peer.CurrentEpoch.NextOutgoingSequence = 1;
                        peer.CurrentEpoch.NextExpectedSequence = 1;
                        peer.CurrentEpoch.PreviousSequenceWindowBitmask = 0;
                        peer.NextEpoch.ClientVerification.CopyTo(peer.CurrentEpoch.ExpectedClientFinishedVerification);
                        peer.NextEpoch.ServerVerification.CopyTo(peer.CurrentEpoch.ServerFinishedVerification);

                        peer.NextEpoch.State = HandshakeState.ExpectingHello;
                        peer.NextEpoch.Handshake?.Dispose();
                        peer.NextEpoch.Handshake = null;
                        peer.NextEpoch.NextOutgoingSequence = 1;
                        peer.NextEpoch.RecordProtection = null;
                        peer.NextEpoch.VerificationStream.Reset();
                        peer.NextEpoch.ClientVerification.SecureClear();
                        peer.NextEpoch.ServerVerification.SecureClear();
                        break;

                    case ContentType.Alert:
                        Logger.Error($"Dropping unsupported Alert record from `{peerAddress}`");
                        break;

                    case ContentType.Handshake:
                        if (!await ProcessHandshake(peer, peerAddress, record, recordPayload)) return;

                        break;

                    case ContentType.ApplicationData:
                        // Forward data to the application
                        await base.ProcessData(new UdpReceiveResult(recordPayload.ToArray(), peerAddress));
                        break;
                }
            }
        }
        peer.Semaphore.Release();
    }

    /// <summary>
    ///     Process an incoming Handshake protocol message
    /// </summary>
    /// <param name="peer">Originating peer</param>
    /// <param name="peerAddress">Peer's network address</param>
    /// <param name="record">Parent record</param>
    /// <param name="message">Record payload</param>
    /// <returns>
    ///     True if further processing of the underlying datagram
    ///     should be continues. Otherwise, false.
    /// </returns>
    private async ValueTask<bool> ProcessHandshake(PeerData peer, IPEndPoint peerAddress, Record record,
        ByteSpan message)
    {
        // Each record may have multiple handshake payloads
        while (message.Length > 0)
        {
            var originalMessage = message;

            if (!Handshake.Handshake.Parse(out var handshake, message))
            {
                Logger.Error($"Dropping malformed Handshake message from `{peerAddress}`");
                return false;
            }

            message = message[Handshake.Handshake.Size..];

            if (message.Length < handshake.Length)
            {
                Logger.Error($"Dropping malformed Handshake message from `{peerAddress}`");
                return false;
            }

            var payload = message[..message.Length];
            message = message[(int)handshake.Length..];
            originalMessage = originalMessage[..(Handshake.Handshake.Size + (int)handshake.Length)];

            // We do not support fragmented handshake messages
            // from the client
            if (handshake.FragmentOffset != 0 || handshake.FragmentLength != handshake.Length)
            {
                Logger.Error(
                    $"Dropping fragmented Handshake message from `{peerAddress}` Offset({handshake.FragmentOffset}) FragmentLength({handshake.FragmentLength}) Length({handshake.Length})");
                continue;
            }

            switch (handshake.MessageType)
            {
                case HandshakeType.ClientHello:
                    if (!await HandleClientHello(peer, peerAddress, record, handshake, originalMessage, payload))
                        return false;
                    break;

                case HandshakeType.ClientKeyExchange:
                    if (peer.NextEpoch.State != HandshakeState.ExpectingClientKeyExchange)
                    {
                        Logger.Error(
                            $"Dropping unexpected ClientKeyExchange message form `{peerAddress}` State({peer.NextEpoch.State})");
                        continue;
                    }

                    if (handshake.MessageSequence != 5)
                    {
                        Logger.Error(
                            $"Dropping bad-sequence ClientKeyExchange message from `{peerAddress}` MessageSequence({handshake.MessageSequence})");
                        continue;
                    }

                    ByteSpan sharedSecret = new byte[peer.NextEpoch.Handshake.SharedKeySize()];
                    if (!peer.NextEpoch.Handshake.VerifyClientMessageAndGenerateSharedKey(sharedSecret, payload))
                    {
                        Logger.Error($"Dropping malformed ClientKeyExchange message from `{peerAddress}`");
                        return false;
                    }

                    // Record incoming ClientKeyExchange message
                    // to verification stream
                    peer.NextEpoch.VerificationStream.AddData(originalMessage);

                    ByteSpan randomSeed = new byte[2 * Random.Size];
                    peer.NextEpoch.ClientRandom.CopyTo(randomSeed);
                    peer.NextEpoch.ServerRandom.CopyTo(randomSeed[Random.Size..]);

                    const int MasterSecretSize = 48;
                    ByteSpan masterSecret = new byte[MasterSecretSize];
                    PrfSha256.ExpandSecret(
                        masterSecret
                        , sharedSecret
                        , PrfLabel.MASTER_SECRET
                        , randomSeed
                    );

                    // Create the record protection for the upcoming epoch
                    switch (peer.NextEpoch.SelectedCipherSuite)
                    {
                        case CipherSuite.TLS_ECDHE_RSA_WITH_AES_128_GCM_SHA256:
                            peer.NextEpoch.RecordProtection = new Aes128GcmRecordProtection(
                                masterSecret
                                , peer.NextEpoch.ServerRandom
                                , peer.NextEpoch.ClientRandom);
                            break;

                        default:
                            Debug.Assert(false,
                                $"How did we agree to a cipher suite {peer.NextEpoch.SelectedCipherSuite} we can't create?");
                            return false;
                    }

                    // Generate verification signatures
                    ByteSpan handshakeStreamHash = new byte[Sha256Stream.DigestSize];
                    peer.NextEpoch.VerificationStream.CalculateHash(handshakeStreamHash);

                    PrfSha256.ExpandSecret(
                        peer.NextEpoch.ClientVerification
                        , masterSecret
                        , PrfLabel.CLIENT_FINISHED
                        , handshakeStreamHash
                    );
                    PrfSha256.ExpandSecret(
                        peer.NextEpoch.ServerVerification
                        , masterSecret
                        , PrfLabel.SERVER_FINISHED
                        , handshakeStreamHash
                    );


                    // Update handshake state
                    masterSecret.SecureClear();
                    peer.NextEpoch.State = HandshakeState.ExpectingChangeCipherSpec;
                    break;

                case HandshakeType.Finished:
                    // Unlike other handshake messages, this is
                    // for the current epoch - not the next epoch

                    // Cannot process a Finished message for
                    // epoch 0
                    if (peer.Epoch == 0)
                    {
                        Logger.Error($"Dropping Finished message for 0-epoch from `{peerAddress}`");
                        continue;
                    }
                    // Cannot process a Finished message when we
                    // are negotiating the next epoch

                    if (peer.NextEpoch.State != HandshakeState.ExpectingHello)
                    {
                        Logger.Error($"Dropping Finished message while negotiating new epoch from `{peerAddress}`");
                        continue;
                    }
                    // Cannot process a Finished message without
                    // verify data

                    if (peer.CurrentEpoch.ExpectedClientFinishedVerification.Length != Finished.Size ||
                        peer.CurrentEpoch.ServerFinishedVerification.Length != Finished.Size)
                    {
                        Debug.Assert(false, "How do we have an established non-zero epoch without verify data?");

                        return false;
                    }
                    // Cannot process a Finished message without
                    // record protection for the previous epoch

                    if (peer.CurrentEpoch.PreviousRecordProtection == null)
                    {
                        Debug.Assert(false,
                            "How do we have an established non-zero epoch with record protection for the previous epoch?");

                        return false;
                    }

                    // Verify message sequence
                    if (handshake.MessageSequence != 6)
                    {
                        Logger.Error(
                            $"Dropping bad-sequence Finished message from `{peerAddress}` MessageSequence({handshake.MessageSequence})");
                        continue;
                    }

                    // Verify the client has the correct
                    // handshake sequence
                    if (payload.Length != Finished.Size)
                    {
                        Logger.Error($"Dropping malformed Finished message from `{peerAddress}`");
                        return false;
                    }

                    if (1 != Const.ConstantCompareSpans(payload, peer.CurrentEpoch.ExpectedClientFinishedVerification))
                    {
                        Logger.Error($"Dropping non-verified Finished Handshake from `{peerAddress}`");

                        // Abort the connection here
                        //
                        // The client is either broken, or
                        // doen not agree on our epoch settings.
                        //
                        // Either way, there is not a feasible
                        // way to progress the connection.
                        // base.MarkConnectionAsStale(peer.ConnectionId);
                        existingPeers.TryRemove(peerAddress, out _);
                        return false;
                    }

                    // Describe our ChangeCipherSpec+Finished
                    var outgoingHandshake = new Handshake.Handshake
                    {
                        MessageType = HandshakeType.Finished,
                        Length = Finished.Size,
                        MessageSequence = 7,
                        FragmentOffset = 0
                    };
                    outgoingHandshake.FragmentLength = outgoingHandshake.Length;

                    var changeCipherSpecRecord = new Record
                    {
                        ContentType = ContentType.ChangeCipherSpec,
                        Epoch = (ushort)(peer.Epoch - 1),
                        SequenceNumber = peer.CurrentEpoch.NextOutgoingSequenceForPreviousEpoch,
                        Length = (ushort)peer.CurrentEpoch.PreviousRecordProtection.GetEncryptedSize(ChangeCipherSpec
                            .Size)
                    };
                    ++peer.CurrentEpoch.NextOutgoingSequenceForPreviousEpoch;

                    var plaintextFinishedPayloadSize = Handshake.Handshake.Size + (int)outgoingHandshake.Length;
                    var finishedRecord = new Record
                    {
                        ContentType = ContentType.Handshake,
                        Epoch = peer.Epoch,
                        SequenceNumber = peer.CurrentEpoch.NextOutgoingSequence,
                        Length = (ushort)peer.CurrentEpoch.RecordProtection.GetEncryptedSize(
                            plaintextFinishedPayloadSize)
                    };
                    ++peer.CurrentEpoch.NextOutgoingSequence;

                    // Encode the flight into wire format
                    ByteSpan packet = new byte[Record.Size + changeCipherSpecRecord.Length + Record.Size +
                                               finishedRecord.Length];
                    var writer = packet;
                    changeCipherSpecRecord.Encode(writer);
                    writer = writer[Record.Size..];
                    ChangeCipherSpec.Encode(writer);

                    var startOfFinishedRecord = packet[(Record.Size + changeCipherSpecRecord.Length)..];
                    writer = startOfFinishedRecord;
                    finishedRecord.Encode(writer);
                    writer = writer[Record.Size..];
                    outgoingHandshake.Encode(writer);
                    writer = writer[Handshake.Handshake.Size..];
                    peer.CurrentEpoch.ServerFinishedVerification.CopyTo(writer);

                    // Protect the ChangeChipherSpec record
                    peer.CurrentEpoch.PreviousRecordProtection.EncryptServerPlaintext(
                        packet.Slice(Record.Size, changeCipherSpecRecord.Length)
                        , packet.Slice(Record.Size, ChangeCipherSpec.Size)
                        , ref changeCipherSpecRecord
                    );

                    // Protect the Finished Handshake record
                    peer.CurrentEpoch.RecordProtection.EncryptServerPlaintext(
                        startOfFinishedRecord.Slice(Record.Size, finishedRecord.Length)
                        , startOfFinishedRecord.Slice(Record.Size, plaintextFinishedPayloadSize)
                        , ref finishedRecord
                    );

                    // Current epoch can now handle application data
                    peer.CanHandleApplicationData = true;

                    await SendData(packet, peerAddress);
                    break;

                // Drop messages that we do not support
                case HandshakeType.CertificateVerify:
                    Logger.Error(
                        $"Dropping unsupported Handshake message from `{peerAddress}` MessageType({handshake.MessageType})");
                    continue;

                // Drop messages that originate from the server
                case HandshakeType.HelloRequest:
                case HandshakeType.ServerHello:
                case HandshakeType.HelloVerifyRequest:
                case HandshakeType.Certificate:
                case HandshakeType.ServerKeyExchange:
                case HandshakeType.CertificateRequest:
                case HandshakeType.ServerHelloDone:
                    Logger.Error(
                        $"Dropping server Handshake message from `{peerAddress}` MessageType({handshake.MessageType})");
                    continue;
            }
        }

        return true;
    }

    /// <summary>
    ///     Handle a ClientHello message for a peer
    /// </summary>
    /// <param name="peer">Originating peer</param>
    /// <param name="peerAddress">Peer address</param>
    /// <param name="record">Parent record</param>
    /// <param name="handshake">Parent Handshake header</param>
    /// <param name="originalMessage"></param>
    /// <param name="payload">Handshake payload</param>
    private async ValueTask<bool> HandleClientHello(PeerData peer, IPEndPoint peerAddress, Record record,
        Handshake.Handshake handshake, ByteSpan originalMessage, ByteSpan payload)
    {
        // Verify message sequence
        if (handshake.MessageSequence != 0)
        {
            Logger.Error(
                $"Dropping bad-sequence ClientHello from `{peerAddress}` MessageSequence({handshake.MessageSequence})`");
            return true;
        }

        // Make sure we can handle a ClientHello message
        if (peer.NextEpoch.State != HandshakeState.ExpectingHello &&
            peer.NextEpoch.State != HandshakeState.ExpectingClientKeyExchange)
            // Always handle ClientHello for epoch 0
            if (record.Epoch != 0)
            {
                Logger.Error($"Dropping ClientHello from `{peer}` Not expecting ClientHello");
                return true;
            }

        if (!ClientHello.Parse(out var clientHello, peer.ProtocolVersion, payload))
        {
            Logger.Error($"Dropping malformed ClientHello Handshake message from `{peerAddress}`");
            return false;
        }

        // Find an acceptable cipher suite we can use
        const CipherSuite selectedCipherSuite = CipherSuite.TLS_ECDHE_RSA_WITH_AES_128_GCM_SHA256;
        if (!clientHello.ContainsCipherSuite(CipherSuite.TLS_ECDHE_RSA_WITH_AES_128_GCM_SHA256) ||
            !clientHello.ContainsCurve(NamedCurve.x25519))
        {
            Logger.Error($"Dropping ClientHello from `{peerAddress}` No compatible cipher suite");
            return false;
        }

        // If this message was not signed by us,
        // request a signed message before doing anything else
        if (!HelloVerifyRequest.VerifyCookie(clientHello.Cookie, peerAddress, currentCookieHmac))
            if (!HelloVerifyRequest.VerifyCookie(clientHello.Cookie, peerAddress, previousCookieHmac))
            {
                ulong outgoingSequence = 1;
                IRecordProtection recordProtection = NullRecordProtection.Instance;
                if (record.Epoch != 0)
                {
                    outgoingSequence = peer.CurrentEpoch.NextExpectedSequence;
                    ++peer.CurrentEpoch.NextOutgoingSequenceForPreviousEpoch;

                    recordProtection = peer.CurrentEpoch.RecordProtection;
                }

                await SendHelloVerifyRequest(peerAddress, outgoingSequence, record.Epoch, recordProtection, peer.ProtocolVersion);
                return true;
            }
        
        // Client is initiating a brand new connection. We need
        // to destroy the existing connection and establish a
        // new session.
        if (record.Epoch == 0 && peer.Epoch != 0)
        {
            var oldConnectionId = peer.ConnectionId;
            peer.ResetPeer(AllocateConnectionId(peerAddress), record.SequenceNumber + 1);

            // Inform the parent layer that the existing
            // connection should be abandoned.
            // base.MarkConnectionAsStale(oldConnectionId);
        }

        // Determine if this is an original message, or a retransmission
        var recordMessagesForVerifyData = false;
        Logger.Verbose("Determine if this is an original message, or a retransmission");
        if (peer.NextEpoch.State == HandshakeState.ExpectingHello)
        {
            // Create our handhake cipher suite
            IHandshakeCipherSuite handshakeCipherSuite;
            if (clientHello.ContainsCurve(NamedCurve.x25519))
            {
                handshakeCipherSuite = new X25519EcdheRsaSha256(random);
            }
            else
            {
                Logger.Error(
                    $"Dropping ClientHello from `{peerAddress}` Could not create TLS_ECDHE_RSA_WITH_AES_128_GCM_SHA256 cipher suite");
                return false;
            }

            peer.Session = clientHello.SessionInfo;

            // Update the state of our epoch transition
            peer.NextEpoch.Epoch = (ushort)(record.Epoch + 1);
            peer.NextEpoch.State = HandshakeState.ExpectingClientKeyExchange;
            peer.NextEpoch.SelectedCipherSuite = selectedCipherSuite;
            peer.NextEpoch.Handshake = handshakeCipherSuite;
            clientHello.Random.CopyTo(peer.NextEpoch.ClientRandom);
            peer.NextEpoch.ServerRandom.FillWithRandom(random);
            recordMessagesForVerifyData = true;

            // Copy the original ClientHello
            // handshake to our verification stream
            peer.NextEpoch.VerificationStream.AddData(
                originalMessage[..(Handshake.Handshake.Size + (int)handshake.Length)]
            );
        }

        // The initial record flight from the server
        // contains the following Handshake messages:
        //    * ServerHello
        //    * Certificate
        //    * ServerKeyExchange
        //    * ServerHelloDone
        //
        // The Certificate message is almost always
        // too large to fit into a single datagram,
        // so it is pre-fragmented
        // (see `SetCertificates`). Therefore, we
        // need to send multiple record packets for
        // this flight.
        //
        // The first record contains the ServerHello
        // handshake message, as well as the first
        // portion of the Certificate message.
        //
        // We then send a record packet until the
        // entire Certificate message has been sent
        // to the client.
        //
        // The final record packet contains the
        // ServerKeyExchange and the ServerHelloDone
        // messages.

        // Describe first record of the flight
        Logger.Verbose("SeverHello");
        var serverHello = new ServerHello
        {
            ServerProtocolVersion = peer.ProtocolVersion,
            Random = peer.NextEpoch.ServerRandom,
            CipherSuite = selectedCipherSuite
        };

        var serverHelloHandshake = new Handshake.Handshake
        {
            MessageType = HandshakeType.ServerHello,
            Length = ServerHello.MinSize,
            MessageSequence = 1,
            FragmentOffset = 0
        };
        serverHelloHandshake.FragmentLength = serverHelloHandshake.Length;

        var maxCertFragmentSize = peer.Session.Version == 0 ? MaxCertFragmentSizeV0 : MaxCertFragmentSizeV1;
        
        /*var certificateData = encodedCertificate;
        var initialCertPadding = Record.Size + Handshake.Handshake.Size + serverHello.Size + Handshake.Handshake.Size;
        var certInitialFragmentSize = Math.Min(certificateData.Length, maxCertFragmentSize - initialCertPadding);

        Logger.Verbose("certificateHandshake");*/
        var certificateHandshake = new Handshake.Handshake
        {
            MessageType = HandshakeType.Certificate,
            Length = encodedCertificatesTotalSize,
            /*Length = (uint)certificateData.Length,*/
            MessageSequence = 2,
            FragmentOffset = 0,
            /*FragmentLength = (uint)certInitialFragmentSize*/
            FragmentLength = (uint)encodedCertificates[0].Length
        };

        var initialRecordPayloadSize = 0
                                       + Handshake.Handshake.Size + serverHello.Size
                                       + Handshake.Handshake.Size + (int)certificateHandshake.FragmentLength
            ;
        var initialRecord = new Record
        {
            ContentType = ContentType.Handshake,
            ProtocolVersion = peer.ProtocolVersion,
            Epoch = peer.Epoch,
            SequenceNumber = peer.CurrentEpoch.NextOutgoingSequence,
            Length = (ushort)peer.CurrentEpoch.RecordProtection.GetEncryptedSize(initialRecordPayloadSize)
        };
        ++peer.CurrentEpoch.NextOutgoingSequence;

        // Convert initial record of the flight to
        // wire format
        Logger.Verbose("Convert initial record of the flight to");
        ByteSpan packet = new byte[Record.Size + initialRecord.Length];
        var writer = packet;
        initialRecord.Encode(writer);
        writer = writer[Record.Size..];
        serverHelloHandshake.Encode(writer);
        writer = writer[Handshake.Handshake.Size..];
        serverHello.Encode(writer);
        writer = writer[ServerHello.MinSize..];
        certificateHandshake.Encode(writer);
        writer = writer[Handshake.Handshake.Size..];
        encodedCertificates[0].CopyTo(writer);
        /*certificateData[..certInitialFragmentSize].CopyTo(writer);
        certificateData = certificateData[certInitialFragmentSize..];*/

        // Protect initial record of the flight
        peer.CurrentEpoch.RecordProtection.EncryptServerPlaintext(
            packet.Slice(Record.Size, initialRecord.Length)
            , packet.Slice(Record.Size, initialRecordPayloadSize)
            , ref initialRecord
        );

        await SendData(packet, peerAddress);

        // Record record payload for verification
        Logger.Verbose("Record record payload for verification");
        if (recordMessagesForVerifyData)
        {
            var fullCeritficateHandshake = certificateHandshake;
            fullCeritficateHandshake.FragmentLength = fullCeritficateHandshake.Length;

            packet = new byte[Handshake.Handshake.Size + ServerHello.MinSize + Handshake.Handshake.Size];
            writer = packet;
            serverHelloHandshake.Encode(writer);
            writer = writer[Handshake.Handshake.Size..];
            serverHello.Encode(writer);
            writer = writer[ServerHello.MinSize..];
            certificateHandshake.Encode(writer);
            writer.Slice(Handshake.Handshake.Size);

            peer.NextEpoch.VerificationStream.AddData(packet);
            /*peer.NextEpoch.VerificationStream.AddData(certificateData);*/
            foreach (var span in encodedCertificates) peer.NextEpoch.VerificationStream.AddData(span);
        }

        // Process additional certificate records
        Logger.Verbose("Process additional certificate records");
        /*const int CertPadding = Record.Size + Handshake.Handshake.Size;
        while (certificateData.Length > 0)
        {
            var certFragmentSize = Math.Min(certificateData.Length, maxCertFragmentSize - CertPadding);
            
            certificateHandshake.FragmentOffset += certificateHandshake.FragmentLength;
            certificateHandshake.FragmentLength = (uint)certFragmentSize;

            var additionalRecordPayloadSize = Handshake.Handshake.Size + (int)certificateHandshake.FragmentLength;
            var additionalRecord = new Record
            {
                ContentType = ContentType.Handshake,
                ProtocolVersion = peer.ProtocolVersion,
                Epoch = peer.Epoch,
                SequenceNumber = peer.CurrentEpoch.NextOutgoingSequence,
                Length = (ushort)peer.CurrentEpoch.RecordProtection.GetEncryptedSize(additionalRecordPayloadSize)
            };
            ++peer.CurrentEpoch.NextOutgoingSequence;

            // Convert record to wire format
            packet = new byte[Record.Size + additionalRecord.Length];
            writer = packet;
            additionalRecord.Encode(writer);
            writer = writer[Record.Size..];
            certificateHandshake.Encode(writer);
            writer = writer[Handshake.Handshake.Size..];
            certificateData[..certFragmentSize].CopyTo(writer);
            certificateData = certificateData[certFragmentSize..];

            // Protect record
            peer.CurrentEpoch.RecordProtection.EncryptServerPlaintext(
                packet.Slice(Record.Size, additionalRecord.Length),
                packet.Slice(Record.Size, additionalRecordPayloadSize),
                ref additionalRecord
                );
            
            await SendData(packet, peerAddress);
        }*/
        for (int ii = 1, nn = encodedCertificates.Count; ii != nn; ++ii)
        {
            certificateHandshake.FragmentOffset += certificateHandshake.FragmentLength;
            certificateHandshake.FragmentLength = (uint)encodedCertificates[ii].Length;

            var additionalRecordPayloadSize = Handshake.Handshake.Size + (int)certificateHandshake.FragmentLength;
            var additionalRecord = new Record
            {
                ContentType = ContentType.Handshake,
                Epoch = peer.Epoch,
                SequenceNumber = peer.CurrentEpoch.NextOutgoingSequence,
                Length = (ushort)peer.CurrentEpoch.RecordProtection.GetEncryptedSize(additionalRecordPayloadSize)
            };
            ++peer.CurrentEpoch.NextOutgoingSequence;

            // Convert record to wire format
            packet = new byte[Record.Size + additionalRecord.Length];
            writer = packet;
            additionalRecord.Encode(writer);
            writer = writer[Record.Size..];
            certificateHandshake.Encode(writer);
            writer = writer[Handshake.Handshake.Size..];
            encodedCertificates[ii].CopyTo(writer);

            // Protect record
            peer.CurrentEpoch.RecordProtection.EncryptServerPlaintext(
                packet.Slice(Record.Size, additionalRecord.Length)
                , packet.Slice(Record.Size, additionalRecordPayloadSize)
                , ref additionalRecord
            );

            await SendData(packet, peerAddress);
        }

        // Describe final record of the flight
        var serverKeyExchangeHandshake = new Handshake.Handshake
        {
            MessageType = HandshakeType.ServerKeyExchange,
            Length = (uint)peer.NextEpoch.Handshake.CalculateServerMessageSize(certificatePrivateKey),
            MessageSequence = 3,
            FragmentOffset = 0
        };
        serverKeyExchangeHandshake.FragmentLength = serverKeyExchangeHandshake.Length;

        var serverHelloDoneHandshake = new Handshake.Handshake
        {
            MessageType = HandshakeType.ServerHelloDone,
            Length = 0,
            MessageSequence = 4,
            FragmentOffset = 0,
            FragmentLength = 0
        };

        var finalRecordPayloadSize = 0
                                     + Handshake.Handshake.Size + (int)serverKeyExchangeHandshake.Length
                                     + Handshake.Handshake.Size + (int)serverHelloDoneHandshake.Length
            ;
        var finalRecord = new Record
        {
            ContentType = ContentType.Handshake,
            Epoch = peer.Epoch,
            SequenceNumber = peer.CurrentEpoch.NextOutgoingSequence,
            Length = (ushort)peer.CurrentEpoch.RecordProtection.GetEncryptedSize(finalRecordPayloadSize)
        };
        ++peer.CurrentEpoch.NextOutgoingSequence;
        Logger.Verbose("finalRecordPayload");

        // Convert final record of the flight to wire
        // format
        packet = new byte[Record.Size + finalRecord.Length];
        writer = packet;
        finalRecord.Encode(writer);
        writer = writer[Record.Size..];
        serverKeyExchangeHandshake.Encode(writer);
        writer = writer[Handshake.Handshake.Size..];
        peer.NextEpoch.Handshake.EncodeServerKeyExchangeMessage(writer, certificatePrivateKey);
        writer = writer[(int)serverKeyExchangeHandshake.Length..];
        serverHelloDoneHandshake.Encode(writer);
        Logger.Verbose("Convert final record of the flight to wire");

        // Record record payload for verification
        if (recordMessagesForVerifyData)
            peer.NextEpoch.VerificationStream.AddData(
                packet.Slice(
                    packet.Offset + Record.Size
                    , finalRecordPayloadSize
                )
            );

        // Protect final record of the flight
        peer.CurrentEpoch.RecordProtection.EncryptServerPlaintext(
            packet.Slice(Record.Size, finalRecord.Length)
            , packet.Slice(Record.Size, finalRecordPayloadSize)
            , ref finalRecord
        );

        await SendData(packet, peerAddress);

        return true;
    }

    /// <summary>
    ///     Handle an incoming packet that is not tied to an existing peer
    /// </summary>
    /// <param name="message">Incoming datagram</param>
    /// <param name="peerAddress">Originating address</param>
    private async ValueTask HandleNonPeerRecord(ByteSpan message, IPEndPoint peerAddress)
    {
        if (!Record.Parse(out var record, null, message))
        {
            Logger.Error($"Dropping malformed record from non-peer `{peerAddress}`");
            return;
        }

        message = message[Record.Size..];

        // The protocol only supports receiving a single record
        // from a non-peer.
        if (record.Length != message.Length)
            if (message.Length < record.Length)
            {
                Logger.Information(
                    $"Dropping bad record from non-peer `{peerAddress}`. Msg length {message.Length} < {record.Length}");
                return;
            }

        // We only accept zero-epoch records from non-peers
        if (record.Epoch != 0) return;

        // We only accept Handshake protocol messages from non-peers
        if (record.ContentType != ContentType.Handshake)
        {
            Logger.Error($"Dropping non-handhsake message from non-peer `{peerAddress}`");
            return;
        }

        var originalMessage = message;

        if (!Handshake.Handshake.Parse(out var handshake, message))
        {
            Logger.Error($"Dropping malformed handshake message from non-peer `{peerAddress}`");
            return;
        }

        // We only accept ClientHello messages from non-peers
        if (handshake.MessageType != HandshakeType.ClientHello)
        {
            Logger.Error($"Dropping non-ClientHello ({handshake.MessageType}) message from non-peer `{peerAddress}`");
            return;
        }

        message = message[Handshake.Handshake.Size..];

        if (!ClientHello.Parse(out var clientHello, null, message))
        {
            Logger.Error($"Dropping malformed ClientHello message from non-peer `{peerAddress}`");
            return;
        }

        // If this ClientHello is not signed by us, request the
        // client send us a signed message
        if (!HelloVerifyRequest.VerifyCookie(clientHello.Cookie, peerAddress, currentCookieHmac))
            if (!HelloVerifyRequest.VerifyCookie(clientHello.Cookie, peerAddress, previousCookieHmac))
            {
                await SendHelloVerifyRequest(peerAddress, 1, 0, NullRecordProtection.Instance, clientHello.ClientProtocolVersion);
                return;
            }

        // Allocate state for the new peer and register it
        var peer = new PeerData(clientHello.ClientProtocolVersion);
        peer.ResetPeer(AllocateConnectionId(peerAddress), record.SequenceNumber + 1);

        existingPeers[peerAddress] = peer;

        await peer.Semaphore.WaitAsync();
        {
            await ProcessHandshake(peer, peerAddress, record, originalMessage);
        }
        peer.Semaphore.Release();
    }

    //Send a HelloVerifyRequest handshake message to a peer
    private ValueTask SendHelloVerifyRequest(IPEndPoint peerAddress, ulong recordSequence, ushort epoch,
        IRecordProtection recordProtection, ProtocolVersion protocolVersion)
    {
        // Do we need to rotate the HMAC key?
        var now = DateTime.UtcNow;
        if (now > nextCookieHmacRotation)
        {
            previousCookieHmac.Dispose();
            previousCookieHmac = currentCookieHmac;
            currentCookieHmac = CreateNewCookieHMAC();
            nextCookieHmacRotation = now + CookieHmacRotationTimeout;
        }

        var handshake = new Handshake.Handshake
        {
            MessageType = HandshakeType.HelloVerifyRequest,
            Length = HelloVerifyRequest.Size,
            MessageSequence = 0,
            FragmentOffset = 0
        };
        handshake.FragmentLength = handshake.Length;

        var plaintextPayloadSize = Handshake.Handshake.Size + (int)handshake.Length;

        var record = new Record
        {
            ContentType = ContentType.Handshake,
            ProtocolVersion = protocolVersion,
            Epoch = epoch,
            SequenceNumber = recordSequence,
            Length = (ushort)recordProtection.GetEncryptedSize(plaintextPayloadSize)
        };

        // Encode record to wire format
        ByteSpan packet = new byte[Record.Size + record.Length];
        var writer = packet;
        record.Encode(writer);
        writer = writer[Record.Size..];
        handshake.Encode(writer);
        writer = writer[Handshake.Handshake.Size..];
        HelloVerifyRequest.Encode(writer, peerAddress, currentCookieHmac, protocolVersion);

        // Protect record payload
        recordProtection.EncryptServerPlaintext(
            packet.Slice(Record.Size, record.Length)
            , packet.Slice(Record.Size, plaintextPayloadSize)
            , ref record
        );

        return SendData(packet, peerAddress);
    }

    // public override ValueTask DisconnectOldConnections(TimeSpan maxAge, MessageWriter disconnectMessage)
    // {
    //     DateTime now = DateTime.UtcNow;
    //     foreach (KeyValuePair<IPEndPoint, PeerData> kvp in this.existingPeers)
    //     {
    //         PeerData peer = kvp.Value;
    //         lock (peer)
    //         {
    //             if (peer.Epoch == 0 || peer.NextEpoch.State != HandshakeState.ExpectingHello)
    //             {
    //                 TimeSpan negotiationAge = now - peer.StartOfNegotiation;
    //                 if (negotiationAge > maxAge)
    //                 {
    //                     base.MarkConnectionAsStale(peer.ConnectionId);
    //                 }
    //             }
    //         }
    //     }
    //
    //     return base.DisconnectOldConnections(maxAge, disconnectMessage);
    // }

    internal void RemovePeerRecord(ConnectionId connectionId)
    {
        existingPeers.TryRemove(connectionId.EndPoint, out _);
    }

    /// <summary>
    ///     Allocate a new connection id
    /// </summary>
    private ConnectionId AllocateConnectionId(IPEndPoint endPoint)
    {
        var rawSerialId = Interlocked.Increment(ref connectionSerial_unsafe);
        return ConnectionId.Create(endPoint, rawSerialId);
    }

    /// <summary>
    ///     Create a new cookie HMAC signer
    /// </summary>
    private static HMAC CreateNewCookieHMAC()
    {
        return new HMACSHA1();
    }

    /// <summary>
    ///     Current state of handshake sequence
    /// </summary>
    private enum HandshakeState
    {
        ExpectingHello,
        ExpectingClientKeyExchange,
        ExpectingChangeCipherSpec,
        ExpectingFinish
    }

    /// <summary>
    ///     State to manage the current epoch `N`
    /// </summary>
    private struct CurrentEpoch
    {
        public ulong NextOutgoingSequence;

        public ulong NextExpectedSequence;
        public ulong PreviousSequenceWindowBitmask;

        public IRecordProtection RecordProtection;
        public IRecordProtection PreviousRecordProtection;

        // Need to keep these around so we can re-transmit our
        // last handshake record flight
        public ByteSpan ExpectedClientFinishedVerification;
        public ByteSpan ServerFinishedVerification;
        public ulong NextOutgoingSequenceForPreviousEpoch;
    }

    /// <summary>
    ///     State to manage the transition from the current
    ///     epoch `N` to epoch `N+1`
    /// </summary>
    private struct NextEpoch
    {
        public ushort Epoch;

        public HandshakeState State;
        public CipherSuite SelectedCipherSuite;

        public ulong NextOutgoingSequence;

        public IHandshakeCipherSuite Handshake;
        public IRecordProtection RecordProtection;

        public ByteSpan ClientRandom;
        public ByteSpan ServerRandom;

        public Sha256Stream VerificationStream;

        public ByteSpan ClientVerification;
        public ByteSpan ServerVerification;
    }

    /// <summary>
    ///     Per-peer state
    /// </summary>
    private sealed class PeerData : IDisposable
    {
        public readonly ProtocolVersion ProtocolVersion;
        public readonly List<ByteSpan> QueuedApplicationDataMessage = [];

        public readonly SemaphoreSlim Semaphore = new(1, 1);
        public bool CanHandleApplicationData;

        public ConnectionId ConnectionId;
        public CurrentEpoch CurrentEpoch;
        public ushort Epoch;
        public NextEpoch NextEpoch;

        public HazelDtlsSessionInfo Session;

        public DateTime StartOfNegotiation;

        public PeerData(ProtocolVersion protocolVersion)
        {
            ProtocolVersion = protocolVersion;
            ByteSpan block = new byte[2 * Finished.Size];
            CurrentEpoch.ServerFinishedVerification = block[..Finished.Size];
            CurrentEpoch.ExpectedClientFinishedVerification = block.Slice(Finished.Size, Finished.Size);

            ResetPeer(ConnectionId.Create(new IPEndPoint(0, 0), 0), 1);
        }

        public void Dispose()
        {
            CurrentEpoch.RecordProtection?.Dispose();
            CurrentEpoch.PreviousRecordProtection?.Dispose();
            NextEpoch.RecordProtection?.Dispose();
            NextEpoch.Handshake?.Dispose();
            NextEpoch.VerificationStream?.Dispose();
        }

        public void ResetPeer(ConnectionId connectionId, ulong nextExpectedSequenceNumber)
        {
            Dispose();

            Epoch = 0;
            CanHandleApplicationData = false;
            QueuedApplicationDataMessage.Clear();

            CurrentEpoch.NextOutgoingSequence = 2; // Account for our ClientHelloVerify
            CurrentEpoch.NextExpectedSequence = nextExpectedSequenceNumber;
            CurrentEpoch.PreviousSequenceWindowBitmask = 0;
            CurrentEpoch.RecordProtection = NullRecordProtection.Instance;
            CurrentEpoch.PreviousRecordProtection = null;
            CurrentEpoch.ServerFinishedVerification.SecureClear();
            CurrentEpoch.ExpectedClientFinishedVerification.SecureClear();

            NextEpoch.State = HandshakeState.ExpectingHello;
            NextEpoch.RecordProtection = null;
            NextEpoch.Handshake = null;
            NextEpoch.ClientRandom = new byte[Random.Size];
            NextEpoch.ServerRandom = new byte[Random.Size];
            NextEpoch.VerificationStream = new Sha256Stream();
            NextEpoch.ClientVerification = new byte[Finished.Size];
            NextEpoch.ServerVerification = new byte[Finished.Size];

            ConnectionId = connectionId;

            StartOfNegotiation = DateTime.UtcNow;
        }
    }
}