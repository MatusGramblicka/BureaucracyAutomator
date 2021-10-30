namespace BureaucracyAutomator2.GitLabContracts
{
    public class Commit
    {
        public string Id { get; set; }
        public string Committer_name { get; set; }
        public string Committer_email { get; set; }
        public string Committed_date { get; set; }
        public string Message { get; set; }
    }
}
