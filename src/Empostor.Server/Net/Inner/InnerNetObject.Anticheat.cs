using System;
using System.Threading.Tasks;
using Empostor.Api;
using Empostor.Api.Innersloth.GameOptions;
using Empostor.Api.Net;
using Empostor.Api.Net.Inner;
using Empostor.Api.Net.Inner.Objects;
using Empostor.Server.Net.Anticheat;
using Empostor.Server.Net.Inner.Objects;

namespace Empostor.Server.Net.Inner
{
    internal abstract partial class InnerNetObject
    {
        protected async ValueTask<bool> ValidateOwnership(CheatContext context, IClientPlayer sender)
        {
            if (!sender.IsOwner(this))
            {
                if (await sender.Client.ReportCheatAsync(context, CheatCategory.Ownership, $"Failed ownership check on {GetType().Name}"))
                {
                    return false;
                }
            }

            return true;
        }

        protected async ValueTask<bool> ValidateHost(CheatContext context, IClientPlayer sender)
        {
            if (!sender.IsHost)
            {
                if (await sender.Client.ReportCheatAsync(context, CheatCategory.MustBeHost, "Failed host check"))
                {
                    return false;
                }
            }

            return true;
        }

        protected async ValueTask<bool> ValidateTarget(CheatContext context, IClientPlayer sender, IClientPlayer? target)
        {
            if (target == null)
            {
                if (await sender.Client.ReportCheatAsync(context, CheatCategory.Target, "Failed target check"))
                {
                    return false;
                }
            }

            return true;
        }

        protected async ValueTask<bool> ValidateBroadcast(CheatContext context, IClientPlayer sender, IClientPlayer? target)
        {
            if (target != null)
            {
                if (await sender.Client.ReportCheatAsync(context, CheatCategory.Target, "Failed broadcast check"))
                {
                    return false;
                }
            }

            return true;
        }

        protected async ValueTask<bool> ValidateCmd(CheatContext context, IClientPlayer sender, IClientPlayer? target)
        {
            if (target == null || !target.IsHost)
            {
                if (await sender.Client.ReportCheatAsync(context, CheatCategory.Target, "Failed cmd check"))
                {
                    return false;
                }
            }

            return true;
        }

        protected async ValueTask<bool> ValidateImpostor(CheatContext context, IClientPlayer sender, InnerPlayerInfo? playerInfo, bool value = true)
        {
            if (playerInfo == null)
            {
                if (await sender.Client.ReportCheatAsync(context, CheatCategory.InvalidObject, "Couldn't check if Impostor, playerInfo not set"))
                {
                    return false;
                }
            }
            else if (playerInfo.IsImpostor != value)
            {
                if (await sender.Client.ReportCheatAsync(context, CheatCategory.Role, "Failed impostor check"))
                {
                    return false;
                }
            }

            return true;
        }

        protected async ValueTask<bool> ValidateCanVent(CheatContext context, IClientPlayer sender, InnerPlayerInfo? playerInfo, bool value = true)
        {
            if (playerInfo == null)
            {
                if (await sender.Client.ReportCheatAsync(context, CheatCategory.InvalidObject, "Couldn't check if can vent, playerInfo not set"))
                {
                    return false;
                }
            }
            else if (playerInfo.CanVent != value)
            {
                if (await sender.Client.ReportCheatAsync(context, CheatCategory.Role, "Failed can vent check"))
                {
                    return false;
                }
            }

            return true;
        }

        protected async ValueTask<bool> ValidateRole(CheatContext context, IClientPlayer sender, InnerPlayerInfo? playerInfo, RoleTypes role)
        {
            if (playerInfo == null)
            {
                if (await sender.Client.ReportCheatAsync(context, CheatCategory.InvalidObject, "Couldn't check role, playerInfo not set"))
                {
                    return false;
                }
            }
            else if (playerInfo.RoleType != role)
            {
                if (await sender.Client.ReportCheatAsync(context, CheatCategory.Role, $"Failed role = {role} check"))
                {
                    return false;
                }
            }

            return true;
        }

        protected async ValueTask<bool> ValidateMurderTiming(CheatContext context, IClientPlayer sender, byte killerPlayerId, IInnerPlayerControl? target)
        {
            if (!Game.AntiCheat.Config.EnableMurderChecks || target == null)
            {
                return true;
            }

            var timeline = Game.AntiCheat.For(killerPlayerId);

            if (target.PlayerInfo is { IsDead: true })
            {
                timeline.DeadTargetKills++;

                if (timeline.DeadTargetKills >= 6 &&
                    await sender.Client.ReportCheatAsync(
                        context,
                        CheatCategory.Murder,
                        $"Client murdered the already dead player {target.PlayerId} {timeline.DeadTargetKills} times"))
                {
                    return false;
                }
            }
            else
            {
                timeline.DeadTargetKills = 0;
            }

            if (Game.Options is not NormalGameOptions options ||
                Game.Options.GameMode is not (GameModes.Normal or GameModes.NormalFools))
            {
                return true;
            }

            var window = options.KillCooldown / 2;

            if (IsFastKill(timeline, target.PlayerId, window, AntiCheatState.Now, out var reason))
            {
                return await sender.Client.ReportCheatAsync(context, CheatCategory.Murder, reason!);
            }

            return true;
        }

        private static bool IsFastKill(PlayerTimeline timeline, byte victimId, double windowSeconds, double now, out string? reason)
        {
            reason = null;

            if (windowSeconds <= 0 || timeline.LastKillAt < 0 || timeline.LastKillTarget == victimId)
            {
                return false;
            }

            if (now - timeline.LastKillAt >= windowSeconds)
            {
                return false;
            }

            reason = $"Killed {victimId} only {now - timeline.LastKillAt:0.###}s after killing {timeline.LastKillTarget}";
            return true;
        }

        protected async ValueTask<bool> ValidateMeetingTiming(CheatContext context, IClientPlayer sender)
        {
            if (!Game.AntiCheat.ShipLoaded)
            {
                return await sender.Client.ReportCheatAsync(
                    context,
                    CheatCategory.Meeting,
                    "Client sent a meeting before the map of this round was spawned");
            }

            if (Game.AntiCheat.SecondsSinceRoundStarted >= 0 &&
                Game.AntiCheat.SecondsSinceRoundStarted < 10)
            {
                return await sender.Client.ReportCheatAsync(
                    context,
                    CheatCategory.Meeting,
                    $"Client sent a meeting {Game.AntiCheat.SecondsSinceRoundStarted:0.###}s into the round (grace 10s)");
            }

            return true;
        }

        protected async ValueTask<bool> ValidateUpdateSystem(CheatContext context, IClientPlayer sender, IMessageReader reader)
        {
            if (!Game.AntiCheat.Config.EnableSabotageChecks)
            {
                return true;
            }

            var position = reader.Position;

            try
            {
                if (!TryReadUpdateSystem(reader, out var systemType, out var playerControl, out var isVent, out var sequenceId, out var state, out var ventId))
                {
                    return await sender.Client.ReportCheatAsync(context, CheatCategory.ProtocolExtension, "Client sent a malformed UpdateSystem RPC");
                }

                if (!sender.IsHost && !playerControl.IsOwnedBy(sender))
                {
                    if (await sender.Client.ReportCheatAsync(context, CheatCategory.Ownership, $"Client sent {nameof(RpcCalls.UpdateSystem)} for a {nameof(InnerPlayerControl)} it does not own"))
                    {
                        return false;
                    }
                }

                var actorId = ResolveActorId(playerControl, sender);

                if (isVent)
                {
                    return await ValidateVentExploit(context, sender, actorId, sequenceId, state, ventId);
                }

                if (!Game.AntiCheat.ShipGraceOver)
                {
                    return true;
                }

                if (!await ValidateSabotageInMeeting(context, sender, systemType))
                {
                    return false;
                }

                if (!await ValidateRapidSabotage(context, sender, actorId, systemType))
                {
                    return false;
                }

                var actorIsImpostor = ResolveActorPlayerInfo(playerControl, sender)?.IsImpostor ?? false;
                return await ValidateSabotageRole(context, sender, actorIsImpostor, systemType, state);
            }
            finally
            {
                reader.Seek(position);
            }
        }

        protected async ValueTask<bool> ValidateVentExploit(CheatContext context, IClientPlayer sender, byte playerId, ushort sequenceId, byte state, byte ventId)
        {
            if (!Game.AntiCheat.Config.EnableVentExploitCheck)
            {
                return true;
            }

            const byte EnterVentState = 2;
            const byte BootFromVentState = 5;
            const ushort ExploitSequenceId = 1;

            var timeline = Game.AntiCheat.For(playerId);
            var now = AntiCheatState.Now;

            if (state == EnterVentState && sequenceId == 0 && ventId == 0)
            {
                timeline.VentExploitPendingAt = now;
                return true;
            }

            if (state != BootFromVentState || sequenceId != ExploitSequenceId || ventId != 0 ||
                timeline.VentExploitPendingAt < 0 ||
                now - timeline.VentExploitPendingAt > 1)
            {
                return true;
            }

            timeline.VentExploitPendingAt = -1;
            return await sender.Client.ReportCheatAsync(
                context,
                CheatCategory.Venting,
                "Client sent the vent kick exploit pair (vent 0, sequence id 0 then 1)");
        }

        protected async ValueTask<bool> ValidateSabotageInMeeting(CheatContext context, IClientPlayer sender, SystemTypes system)
        {
            if (!Game.AntiCheat.MeetingGuardArmed)
            {
                return true;
            }

            return await sender.Client.ReportCheatAsync(
                context,
                CheatCategory.Sabotage,
                $"Client sabotaged {system} while a meeting is in progress");
        }

        protected async ValueTask<bool> ValidateRapidSabotage(CheatContext context, IClientPlayer sender, byte playerId, SystemTypes system)
        {
            if ((int)system == 16)
            {
                return true;
            }

            var timeline = Game.AntiCheat.For(playerId);
            var now = AntiCheatState.Now;

            if (timeline.LastSabotageAt >= 0 &&
                now - timeline.LastSabotageAt < 0.1 &&
                timeline.LastSabotageSystem != (int)system)
            {
                var reason = $"Sabotaged {system} {(now - timeline.LastSabotageAt) * 1000:0.###}ms after {timeline.LastSabotageSystem}";
                timeline.LastSabotageAt = now;
                timeline.LastSabotageSystem = (int)system;
                return await sender.Client.ReportCheatAsync(context, CheatCategory.Sabotage, reason);
            }

            timeline.LastSabotageAt = now;
            timeline.LastSabotageSystem = (int)system;
            return true;
        }

        protected async ValueTask<bool> ValidateSabotageRole(CheatContext context, IClientPlayer sender, bool actorIsImpostor, SystemTypes system, byte state)
        {
            if (actorIsImpostor)
            {
                return true;
            }

            var startingSabotage = (state & 0x80) != 0;
            if (!startingSabotage && system != SystemTypes.MushroomMixupSabotage)
            {
                return true;
            }

            var reason = startingSabotage
                ? $"Non-impostor started a sabotage of {system} (state 0x{state:X2})"
                : $"Non-impostor triggered {system}";

            return await sender.Client.ReportCheatAsync(context, CheatCategory.Sabotage, reason);
        }

        protected async ValueTask<bool> ValidateVoteCast(CheatContext context, IClientPlayer sender)
        {
            if (!Game.AntiCheat.Config.EnableVotingChecks)
            {
                return true;
            }

            if (!Game.AntiCheat.InMeeting &&
                await sender.Client.ReportCheatAsync(context, CheatCategory.Voting, "Client cast a vote while no meeting is in progress"))
            {
                return false;
            }

            if (sender.Character?.PlayerInfo is { IsDead: true } &&
                await sender.Client.ReportCheatAsync(context, CheatCategory.Voting, "Client cast a vote while being dead"))
            {
                return false;
            }

            return true;
        }

        protected async ValueTask<bool> ValidateVoterCount(CheatContext context, IClientPlayer sender, IMessageReader reader)
        {
            var position = reader.Position;
            int voters;

            try
            {
                voters = reader.ReadPackedInt32();
            }
            catch (Exception)
            {
                return true;
            }
            finally
            {
                reader.Seek(position);
            }

            var limit = Math.Max(Game.Options.MaxPlayers, Game.PlayerCount);
            if (voters >= 0 && voters <= limit && voters <= reader.Length - position)
            {
                return true;
            }

            return await sender.Client.ReportCheatAsync(
                context,
                CheatCategory.ProtocolExtension,
                $"Client sent a VotingComplete with {voters} voters (max {limit})");
        }

        internal static async ValueTask<bool> ValidateGameDataTag(CheatContext context, IClientPlayer sender, byte tag)
        {
            if (await sender.Client.ReportCheatAsync(context, CheatCategory.ProtocolExtension, $"Client sent an unknown game data tag {tag}"))
            {
                return false;
            }

            return true;
        }

        private bool TryReadUpdateSystem(
            IMessageReader reader,
            out SystemTypes systemType,
            out InnerPlayerControl playerControl,
            out bool isVent,
            out ushort sequenceId,
            out byte state,
            out byte ventId)
        {
            systemType = SystemTypes.Hallway;
            playerControl = null!;
            isVent = false;
            sequenceId = 0;
            state = 0;
            ventId = byte.MaxValue;

            if (reader.Position >= reader.Length)
            {
                return false;
            }

            systemType = (SystemTypes)reader.ReadByte();

            var control = reader.ReadNetObject<InnerPlayerControl>(Game);
            if (control == null)
            {
                return false;
            }

            playerControl = control;

            if (systemType == SystemTypes.Ventilation)
            {
                if (reader.Length - reader.Position < 4)
                {
                    return false;
                }

                isVent = true;
                sequenceId = reader.ReadUInt16();
                state = reader.ReadByte();
                ventId = reader.ReadByte();
                return true;
            }

            if (reader.Position >= reader.Length)
            {
                return false;
            }

            if (systemType == SystemTypes.Sabotage)
            {
                if (reader.Length - reader.Position < 2)
                {
                    return false;
                }

                systemType = (SystemTypes)reader.ReadByte();
            }

            state = reader.ReadByte();
            return true;
        }

        private byte ResolveActorId(InnerPlayerControl control, IClientPlayer sender)
        {
            if (control.OwnerId >= 0)
            {
                var owner = Game.GetClientPlayer(control.OwnerId);
                if (owner?.Character != null)
                {
                    return owner.Character.PlayerId;
                }
            }

            return sender.Character?.PlayerId ?? byte.MaxValue;
        }

        private InnerPlayerInfo? ResolveActorPlayerInfo(InnerPlayerControl control, IClientPlayer sender)
        {
            if (control.PlayerInfo != null)
            {
                return control.PlayerInfo;
            }

            var owner = control.OwnerId >= 0 ? Game.GetClientPlayer(control.OwnerId) : null;
            return (InnerPlayerInfo?)(owner?.Character?.PlayerInfo ?? sender.Character?.PlayerInfo);
        }

        protected async ValueTask<bool> UnregisteredCall(CheatContext context, IClientPlayer sender)
        {
            if (await sender.Client.ReportCheatAsync(context, CheatCategory.ProtocolExtension, "Client sent unregistered call"))
            {
                return false;
            }

            return true;
        }
    }
}
