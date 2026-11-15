namespace Empostor.Api.Service.Admin.Verify
{
    public interface IVerifyStore
    {
        void AddPending(string friendCode, string userOpenid);

        bool ValidateSecret(string secret);

        bool TryConfirm(string friendCode, string userOpenid);
    }
}
