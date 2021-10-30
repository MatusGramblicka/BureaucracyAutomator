namespace BureaucracyAutomator2.GitLabContracts
{
    public class Branch
    {
        public string Name { get; set; }
        public CommitOnBranch Commit { get; set; }
    }
}
