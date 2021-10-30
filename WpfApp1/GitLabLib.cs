using BureaucracyAutomator2.GitLabContracts;
using Newtonsoft.Json;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace BureaucracyAutomator2
{
    public static class GitLabLib
    {
        private static readonly HttpClient client = new HttpClient();        
        static readonly string userEndpointGitLab = "https://git.kistler.com/api/v4/user";
        static readonly string projectsEndpointGitLab = "https://git.kistler.com/api/v4/projects";
        static readonly string dayTimeFormating = "yyyy-MM-ddTHH:mm:ssz";
        static ConcurrentBag<CommitOnProject> cb = new ConcurrentBag<CommitOnProject>();
        static ConcurrentBag<Project>  projectsBagCompleteCleaned =  new ConcurrentBag<Project>();

        public static async Task<bool> TestGitlabAccess(string accessToken)
        {
            if (!client.DefaultRequestHeaders.Contains("PRIVATE-TOKEN"))
                client.DefaultRequestHeaders.Add("PRIVATE-TOKEN", accessToken);

            try
            {
                var clientResult = await client.GetStringAsync(userEndpointGitLab);
            }
            catch (Exception ex)
            {
                return false;
            }
            finally
            { 
                client.DefaultRequestHeaders.Remove("PRIVATE-TOKEN");
            }

            return true;
        }

        public static async Task<List<CommitOnProject>> GetGitlabData(string accessToken)
        {
            var projectsListComplete = new List<Project>();
            var projectsData = new List<Project>();
            var commitsListWithProjectComplete = new List<CommitOnProject>();
            var commitsListWithProjectCompleteFromMergeRequests = new List<CommitOnProject>();
            var branchesData = new List<Branch>();
            var membersData = new List<Member>();
            var bagAddTasks2 = new List<Task>();
            var projectsListCompleteCleaned2 = new List<Project>();
            var bagAddTasks = new List<Task>();

            var threadsNum = Environment.ProcessorCount * 2;

            if (!client.DefaultRequestHeaders.Contains("PRIVATE-TOKEN"))
                client.DefaultRequestHeaders.Add("PRIVATE-TOKEN", accessToken);

            var clientResult = await client.GetStringAsync(userEndpointGitLab);
            var user = await Task.Run(() =>
                    JsonConvert.DeserializeObject<User>(clientResult));
            
            var today = DateTime.Now;
            DateTime startDate = new DateTime(today.Year, today.Month, 1);
            var currentMonthStart = startDate.ToString(dayTimeFormating);

            int index = 1;
            do
            {
                var projectsResult = await client.GetStringAsync(projectsEndpointGitLab + $"?per_page=100&page={index}&last_activity_after={currentMonthStart}");
                projectsData = await Task.Run(() =>
                        JsonConvert.DeserializeObject<List<Project>>(projectsResult));
                projectsListComplete.AddRange(projectsData);
                index += 1;
            } while (projectsData.Count != 0);            

            var dividedForTasks2 = 1;            
            if (projectsListComplete.Count > threadsNum)                
                dividedForTasks2 = projectsListComplete.Count / threadsNum;

            int indexProjectlist;
            for (indexProjectlist = 0; indexProjectlist < projectsListComplete.Count - dividedForTasks2; indexProjectlist += dividedForTasks2)
            {
                bagAddTasks2.Add(CleanProjects(indexProjectlist, indexProjectlist + dividedForTasks2, projectsListComplete, user));
            }
            bagAddTasks2.Add(CleanProjects(indexProjectlist, projectsListComplete.Count, projectsListComplete, user));

            await Task.WhenAll(bagAddTasks2.ToArray());
            
            projectsListCompleteCleaned2 = projectsBagCompleteCleaned.ToList();            

            var dividedForTasks = 1;
            
            if (projectsListCompleteCleaned2.Count > threadsNum)
                dividedForTasks = projectsListCompleteCleaned2.Count / threadsNum;

            int indexProjectsCompleted;
            for (indexProjectsCompleted = 0; indexProjectsCompleted < projectsListCompleteCleaned2.Count - dividedForTasks; indexProjectsCompleted += dividedForTasks)
            {
                bagAddTasks.Add(WriteCommits(indexProjectsCompleted, indexProjectsCompleted + dividedForTasks, projectsListCompleteCleaned2, user));
            }
            bagAddTasks.Add(WriteCommits(indexProjectsCompleted, projectsListCompleteCleaned2.Count, projectsListCompleteCleaned2, user));

            await Task.WhenAll(bagAddTasks.ToArray());              

            var commitsListWithProjectCompleteDistinct = cb
                .GroupBy(x => x.Id).Select(y => y.First())
                .ToList();            

            foreach (var project in projectsListCompleteCleaned2)
            {
                var mergeRequestsData = new List<MergeRequest>();
                var mergeRequestsListComplete = new List<MergeRequest>();
                int indexMergeRequests = 1;
                try
                {
                    do
                    {
                        var mergeRequestsResult = await client.GetStringAsync(projectsEndpointGitLab + $"/{project.Id}/merge_requests?state=all&" +
                            $"created_after={currentMonthStart}&per_page=100&page={indexMergeRequests}");
                        mergeRequestsData = await Task.Run(() =>
                            JsonConvert.DeserializeObject<List<MergeRequest>>(mergeRequestsResult));
                        mergeRequestsListComplete.AddRange(mergeRequestsData);
                        indexMergeRequests += 1;
                    } while (mergeRequestsData.Count != 0);
                }
                catch (Exception ex)
                {
                    continue;
                }

                foreach (var mergeRequest in mergeRequestsListComplete)
                {
                    var commitsData = new List<Commit>();
                    int indexCommits = 1;
                    do
                    {
                        try
                        {
                            var commitsResult = await client.GetStringAsync(projectsEndpointGitLab + $"/{project.Id}/merge_requests/{mergeRequest.Iid}/commits?per_page=100&page={indexCommits}");
                            commitsData = await Task.Run(() =>
                                JsonConvert.DeserializeObject<List<Commit>>(commitsResult));
                        }
                        catch (Exception ex)
                        {
                            continue;
                        }

                        commitsData = commitsData
                            .Where(c => c.Committer_email == user.Email || c.Committer_name.ToLower() == user.Name.ToLower())
                            .ToList();

                        foreach (var commit in commitsData)
                        {
                            _ = DateTime.TryParse(commit.Committed_date, out var committedDate);

                            if (committedDate < startDate)
                                continue;

                            commitsListWithProjectCompleteFromMergeRequests.Add(new CommitOnProject
                            {
                                Committed_date = commit.Committed_date,
                                Id = commit.Id,
                                Name = project.Name,
                                Message = commit.Message
                            });
                        }

                        indexCommits += 1;
                    } while (commitsData.Count != 0);
                }
            }

            var commitsListWithProjectCompleteFromMergeRequestsDistinct = commitsListWithProjectCompleteFromMergeRequests
                .GroupBy(x => x.Id).Select(y => y.First())
                .ToList();

            commitsListWithProjectCompleteDistinct.AddRange(commitsListWithProjectCompleteFromMergeRequestsDistinct);

            var commitsListWithProjectCompleteDistinct2 = commitsListWithProjectCompleteDistinct
                .GroupBy(x => x.Id).Select(y => y.First())
                .ToList();

            client.DefaultRequestHeaders.Remove("PRIVATE-TOKEN");

            return commitsListWithProjectCompleteDistinct2;
        }

        private static async Task WriteCommits(int start, int end, List<Project> projects, User user)
        {            
            var today = DateTime.Now;
            DateTime startDate = new DateTime(today.Year, today.Month, 1);
            var currentMonthStart = startDate.ToString(dayTimeFormating);

            for (int i = start; i < end; i++)
            {
                var branchesData = new List<Branch>();
                var branchesListComplete = new List<Branch>();
                int indexBranches = 1;
                try
                {
                    do
                    {
                        var branchesResult = await client.GetStringAsync(projectsEndpointGitLab + $"/{projects[i].Id}/repository/branches?per_page=100&page={indexBranches}");
                        branchesData = await Task.Run(() =>
                            JsonConvert.DeserializeObject<List<Branch>>(branchesResult));
                        branchesListComplete.AddRange(branchesData);
                        indexBranches += 1;
                    } while (branchesData.Count != 0);
                }
                catch (Exception ex)
                {
                    continue;
                }

                foreach (var branch in branchesListComplete)
                {
                    _ = DateTime.TryParse(branch.Commit.Created_at, out var branchLastCommitDate);

                    if (branchLastCommitDate < startDate)
                        continue;

                    var commitsData = new List<Commit>();
                    int indexCommits = 1;
                    do
                    {
                        try
                        {
                            var commitsResult = await client.GetStringAsync(projectsEndpointGitLab + $"/{projects[i].Id}/repository/commits?ref_name={branch.Name}" +
                                $"&since={currentMonthStart}&per_page=100&page={indexCommits}");
                            commitsData = await Task.Run(() =>
                                JsonConvert.DeserializeObject<List<Commit>>(commitsResult));
                        }
                        catch (Exception ex)
                        {
                            continue;
                        }

                        commitsData = commitsData
                            .Where(c => c.Committer_email == user.Email || c.Committer_name.ToLower() == user.Name.ToLower())
                            .ToList();

                        foreach (var commit in commitsData)
                        {
                            cb.Add(new CommitOnProject
                            {
                                Committed_date = commit.Committed_date,
                                Id = commit.Id,
                                Name = projects[i].Name,
                                Message = commit.Message
                            });
                        }

                        indexCommits += 1;
                    } while (commitsData.Count != 0);
                }
            }
        }

        private static async Task CleanProjects(int start, int end, List<Project> projects, User user)
        {
            for (int i = start; i < end; i++)
            {
                var membersData = new List<Member>();
                var membersListComplete = new List<Member>();
                int indexMembers = 1;

                do
                {
                    var membersResult = await client.GetStringAsync(projectsEndpointGitLab + $"/{projects[i].Id}/members/all?per_page=100&page={indexMembers}");
                    membersData = await Task.Run(() =>
                            JsonConvert.DeserializeObject<List<Member>>(membersResult));
                    membersListComplete.AddRange(membersData);
                    indexMembers += 1;
                } while (membersData.Count != 0);

                var data = membersListComplete.Where(m => m.Name.ToLower() == user.Name.ToLower() || m.Username.ToLower() == user.Name.ToLower()).ToList().Count;
                if (data > 0)
                {
                    projectsBagCompleteCleaned.Add(projects[i]);
                }
            }
        }
    }
}
