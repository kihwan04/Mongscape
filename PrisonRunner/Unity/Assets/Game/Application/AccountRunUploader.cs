namespace Muhanok.Application
{
    // Saves a member's finished runs. A failed save stays in memory and is sent once more before the next run;
    // the server ignores a resent id, so a retry never credits coins twice.
    public sealed class AccountRunUploader
    {
        private sealed class Pending { public string AccountId; public AccountRun Run; }
        private readonly AccountSession account;
        private Pending pending;
        public IAccountGateway Gateway { get; set; }
        // The last run could not be saved (shown on the result screen).
        public bool LastFailed { get; private set; }
        public bool HasPending => pending != null;
        public AccountRunUploader(AccountSession session, IAccountGateway gateway) { account = session; Gateway = gateway; }

        // saved(ok) answers once for this run, after the server replied (the result screen then asks for /stats).
        public void Save(AccountRun run, System.Action<bool> saved = null)
        {
            if (run == null || Gateway == null || !account.IsMember) { saved?.Invoke(false); return; }
            string id = account.Id; LastFailed = false;
            var retry = pending; pending = null;
            // The older run goes first so the balance from the newest reply is the latest one.
            if (retry != null && retry.AccountId == id) Gateway.SaveRun(account.Token, retry.Run, reply => { Finished(id, reply, null); Send(id, run, saved); });
            else Send(id, run, saved);
        }
        private void Send(string id, AccountRun run, System.Action<bool> saved)
        {
            if (!account.IsMember || account.Id != id) { Keep(id, run); saved?.Invoke(false); return; }
            Gateway.SaveRun(account.Token, run, reply => { Finished(id, reply, run); saved?.Invoke(reply != null && reply.ok); });
        }
        private void Finished(string id, AccountReply reply, AccountRun run)
        {
            if (reply != null && reply.ok) { if (account.IsMember && account.Id == id) account.SetCoins(reply.coins); return; }
            if (run != null) Keep(id, run);
        }
        private void Keep(string id, AccountRun run) { pending = new Pending { AccountId = id, Run = run }; LastFailed = true; }
    }
}
