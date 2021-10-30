using BureaucracyAutomator2;
using BureaucracyAutomator2.Contracts;
using BureaucracyAutomator2.GitLabContracts;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace WpfApp1
{
    public partial class MainWindow : Window
    {
        //https://www.thebestcsharpprogrammerintheworld.com/2018/04/19/resize-wpf-contents-controls-when-window-is-resized/
        List<DayAndTime> listActualTimes = new List<DayAndTime>();
        List<Exchange> appointments = new List<Exchange>();
        List<CommitOnProject> gitlabData = new List<CommitOnProject>();
        DateTime startDate;
        int daysInMonth;

        public MainWindow()
        {
            InitializeComponent();

            startDate = new DateTime(DateTime.Now.Year, DateTime.Now.Month, 1);
            daysInMonth = DateTime.DaysInMonth(DateTime.Now.Year, DateTime.Now.Month);
        }       

        private async void Button_Click(object sender, RoutedEventArgs e)
        {
            int shiftDown = 70;
            var userName = InterflexUserName.Text;
            var password = InterflexPassword.Password;

            if (userName.Length > 0 && password.Length > 0)
            {
                if ((bool) chBInterflexApi.IsChecked)
                {
                    // if docker selenium is running
                    // get request
                    // curl -X POST "http://localhost:3001/interflex" -H  "accept: text/plain" -H  "Content-Type: application/json" -d "{\"user\":\"userName\",\"password\":\"somePasswrd\"}"
                    var client = new HttpClient
                    {
                        BaseAddress = new Uri("http://localhost:3001/")
                    };

                    // Add an Accept header for JSON format.
                    client.DefaultRequestHeaders.Accept.Add(
                        new MediaTypeWithQualityHeaderValue("application/json"));

                    var userInterflex = new UserInterflex
                    {
                        User = userName,
                        Password = password
                    };

                    try
                    {
                        var response = await client.PostAsJsonAsync("interflex", userInterflex);

                        // todo if credentials ar wrong check for result if empty notify user

                        if (response.IsSuccessStatusCode)
                        {
                            var responseSerialized = await response.Content.ReadAsStringAsync();
                            listActualTimes = JsonConvert.DeserializeObject<List<DayAndTime>>(responseSerialized);
                        }
                        else
                        {
                            MessageBox.Show("Error Code" +
                                            response.StatusCode + " : Message - " + response.ReasonPhrase);
                        }
                    }
                    catch (HttpRequestException ex)
                    {
                        Console.WriteLine(ex);
                        MessageBox.Show($"Error: {ex.InnerException?.Message}");
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine(ex);
                        MessageBox.Show($"Error: {ex.InnerException?.Message}");
                    }
                }
                else
                {
                    // if no docker selenium is running

                    listActualTimes =
                        await System.Threading.Tasks.Task.Run(() => InterflexLib.GetInterflexData(userName, password));
                }
            }

            if (listActualTimes.Count > 0)
            {
                foreach (var actualTime in listActualTimes)
                {
                    var canva = new Canvas();
                    canva.HorizontalAlignment = HorizontalAlignment.Left;
                    canva.VerticalAlignment = VerticalAlignment.Top;
                    canva.Width = 350;
                    canva.Height = 100;
                    canva.Margin = new Thickness(30, shiftDown, 0, 0);
                    canva.Background = new SolidColorBrush(Colors.AliceBlue);

                    var label = new Label();
                    label.Content = actualTime.ActualTime;
                    label.Margin = new Thickness(20, 25, 0, 0);
                    label.Height = 100;
                    label.Width = 140;
                    label.FontSize = 30;
                    label.FontFamily = new FontFamily("Courier New");
                    canva.Children.Add(label);

                    var label3 = new Label();
                    if (actualTime.AbsenceReasons.Count > 0)
                    {
                        foreach (var absenceReason in actualTime.AbsenceReasons)
                        {
                            if (absenceReason.Duration.HasValue)
                            {
                                var durationRounded = Math.Round((decimal)absenceReason.Duration, 2);
                                label3.Content += absenceReason.Absence + "\t" + durationRounded.ToString() + "\n";
                            }
                            else
                                label3.Content += absenceReason.Absence + "\t" + absenceReason.Duration.ToString() + "\n";
                        }
                    }

                    label3.Margin = new Thickness(160, 25, 0, 0);
                    label3.Height = 100;
                    label3.Width = 180;
                    label3.FontSize = 10;
                    canva.Children.Add(label3);

                    var label2 = new Label();
                    var dateString = DateTime.Now.Month.ToString() + "." + actualTime.Day.Split(",")[1] + ".";
                    DateTime.TryParse(dateString, out var actualDate);
                    if (actualDate.DayOfWeek.ToString() == "Saturday" || actualDate.DayOfWeek.ToString() == "Sunday")
                    {
                        canva.Background = new SolidColorBrush(Colors.Gray);
                    }
                    label2.Content = actualDate.ToString("dd.MM.");
                    label2.Margin = new Thickness(0, 0, 0, 0);
                    label2.Height = 100;
                    label2.Width = 500;                    
                    label2.FontWeight = FontWeights.Bold;
                    canva.Children.Add(label2);

                    MainGrid.Children.Add(canva);

                    shiftDown += 110;
                }
            }
            else
                MessageBox.Show("Error: no data gathered, please check credentials");
        }

        private async void Button_Click_1(object sender, RoutedEventArgs e)
        {
            int shiftDown = 70;
            var userName = ExchangeUserName.Text;
            var password = ExchangePassword.Password;
            bool success = false;

            if (userName.Length > 0 && password.Length > 0)
                (appointments, success) = await System.Threading.Tasks.Task.Run(() => ExchangeLib.GetExchangeData(userName, password));

            if (appointments.Count > 0)
            {
                for (int i = 0; i < daysInMonth; i++)
                {
                    var date = startDate.AddDays(i);
                    var dateString = date.ToString("yyyy-MM-dd");
                    if (appointments.Where(l => l.Start.ToString().Split("T")[0] == dateString).ToList().Count == 0)
                    {
                        DateTime.TryParse(dateString, out var exchangeStart);
                        appointments.Add(new Exchange() { Start = exchangeStart });
                    }
                }

                var groups = appointments.GroupBy(g => g.Start.Day)
                    .OrderBy(o => o.Key)
                    .ToList();

                foreach (var nameGroup in groups)
                {
                    var canva = new Canvas();
                    canva.HorizontalAlignment = HorizontalAlignment.Left;
                    canva.VerticalAlignment = VerticalAlignment.Top;
                    canva.Width = 450;
                    canva.Height = 100;
                    canva.Margin = new Thickness(400, shiftDown, 0, 0);
                    canva.Background = new SolidColorBrush(Colors.AliceBlue);
                    var label = new Label();

                    var label2 = new Label();

                    foreach (var exch in nameGroup)
                    {
                        string space = new string(' ', 40);
                        string firstPart = "";
                        string secondPart = "";
                        if (exch.Subject != null)
                        {
                            firstPart = exch.Subject;
                            int length = exch.Subject.Length;
                            if (length > 40)
                            {
                                firstPart = exch.Subject.Substring(0, 40);
                                secondPart = exch.Subject.Substring(40, length > 80 ? 40 : length -41);
                                label.Content += firstPart + "\n";
                                label.Content += secondPart + new string(' ', space.Length - secondPart.Length) + Math.Round(exch.Duration.TotalHours, 2).ToString() + "\n";
                            }
                            else
                                label.Content += exch.Subject.ToString() + new string(' ', space.Length - length) + Math.Round(exch.Duration.TotalHours, 2).ToString() + "\n";
                        }
                        label2.Content = exch.Start.ToString("dd.MM.");

                        if (exch.Start.DayOfWeek.ToString() == "Saturday" || exch.Start.DayOfWeek.ToString() == "Sunday")
                        {
                            canva.Background = new SolidColorBrush(Colors.Gray);
                        }
                    }

                    label.Margin = new Thickness(60, 0, 0, 0);
                    label.Height = 100;
                    label.Width = 500;
                    label.FontFamily = new FontFamily("Courier New");
                    canva.Children.Add(label);


                    label2.Margin = new Thickness(0, 0, 0, 0);
                    label2.Height = 100;
                    label2.Width = 500;
                    canva.Children.Add(label2);

                    MainGrid.Children.Add(canva);

                    shiftDown += 110;
                }
            }
        }
        
        private async void Button_Click_2(object sender, RoutedEventArgs e)
        {
            int shiftDown = 70;

            var accessToken = AccessToken.Text;

            if (accessToken.Length > 0)
                gitlabData = await System.Threading.Tasks.Task.Run(() => GitLabLib.GetGitlabData(accessToken));

            if (gitlabData.Count > 0)
            {
                for (int i = 0; i < daysInMonth; i++)
                {
                    var date = startDate.AddDays(i);
                    var dateString = date.ToString("yyyy-MM-dd");
                    if (gitlabData.Where(l => l.Committed_date.Split("T")[0] == dateString).ToList().Count == 0)
                    {
                        gitlabData.Add(new CommitOnProject() { Committed_date = dateString });
                    }
                }

                var gitlabGroups = gitlabData.GroupBy(g => g.Committed_date.Split("T")[0])
                    .OrderBy(o => o.Key)
                    .ToList();

                foreach (var gitlabGroup in gitlabGroups)
                {
                    var canva = new Canvas();
                    canva.HorizontalAlignment = HorizontalAlignment.Left;
                    canva.VerticalAlignment = VerticalAlignment.Top;
                    canva.Width = 590;
                    canva.Height = 100;
                    canva.Margin = new Thickness(870, shiftDown, 0, 0);
                    canva.Background = new SolidColorBrush(Colors.AliceBlue);
                    var textBlock = new TextBlock();

                    var label2 = new Label();

                    foreach (var gitlab in gitlabGroup)
                    {
                        if (gitlab.Id != null || gitlab.Name != null)
                        {
                            textBlock.Text += gitlab.Name  + "\t" + gitlab.Message.Replace("\n", "") + "\n";
                        }

                        DateTime.TryParse(gitlab.Committed_date, out var date);
                        label2.Content = date.ToString("dd.MM.");

                        if (date.DayOfWeek.ToString() == "Saturday" || date.DayOfWeek.ToString() == "Sunday")
                        {
                            canva.Background = new SolidColorBrush(Colors.Gray);
                        }
                    }

                    textBlock.Margin = new Thickness(60, 0, 0, 0);
                    textBlock.HorizontalAlignment = HorizontalAlignment.Stretch;
                    textBlock.FontFamily = new FontFamily("Courier New");

                    ScrollViewer sv = new ScrollViewer();
                    sv.HorizontalScrollBarVisibility = ScrollBarVisibility.Auto;
                    sv.VerticalScrollBarVisibility = ScrollBarVisibility.Auto;
                    sv.Height = 100;
                    sv.Width = 585;
                    sv.HorizontalAlignment = HorizontalAlignment.Left;
                    sv.VerticalAlignment = VerticalAlignment.Top;
                    sv.Margin = new Thickness(870, shiftDown, 0, 0);
                    sv.CanContentScroll = true;
                    sv.Content = textBlock;

                    label2.Margin = new Thickness(0, 0, 0, 0);
                    label2.Height = 100;
                    label2.Width = 500;
                    canva.Children.Add(label2);

                    MainGrid.Children.Add(canva);
                    MainGrid.Children.Add(sv);
                    

                    shiftDown += 110;
                }
            }
        }

        private async void Button_Click_3(object sender, RoutedEventArgs e)
        {
            var userName = ExchangeUserName.Text;
            var password = ExchangePassword.Password;
            bool success = false;

            success = await System.Threading.Tasks.Task.Run(() => ExchangeLib.TestConnection(userName, password));

            if (success)
            {
                lblTestConnExchange.Foreground = Brushes.Green;
                lblTestConnExchange.Content = "OK";
            }
            else
            {
                lblTestConnExchange.Content = "NOK";
                lblTestConnExchange.Foreground = Brushes.Red;
            }
        }

        private async void GitLabTestConn_Click(object sender, RoutedEventArgs e)
        {
            var accessToken = AccessToken.Text;
            bool success = false;

            if (accessToken.Length > 0)
                success = await System.Threading.Tasks.Task.Run(() => GitLabLib.TestGitlabAccess(accessToken));

            if (success)
            {
                lblGitLabTestConn.Foreground = Brushes.Green;
                lblGitLabTestConn.Content = "OK";
            }
            else
            {
                lblGitLabTestConn.Content = "NOK";
                lblGitLabTestConn.Foreground = Brushes.Red;
            }
        }
    }
}
