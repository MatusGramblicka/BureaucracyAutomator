using BureaucracyAutomator2.Contracts;
using OpenQA.Selenium;
using OpenQA.Selenium.Chrome;
using OpenQA.Selenium.Support.UI;
using System;
using System.Collections.Generic;
using System.Linq;

namespace BureaucracyAutomator2
{
    public class InterflexLib
    {
        const string URI = "https://interflex.kistler.com:8443/WebClient/iflx/login.jsp?userlocale=en";
        private static IWebDriver _driver;

        public static List<DayAndTime> GetInterflexData(string username, string password)
        {
            var listActualTimes = new List<DayAndTime>();
            try
            {
                ChromeOptions options = new ChromeOptions();
                options.AddArguments("--headless");
                _driver = new ChromeDriver(options);

                //FirefoxOptions options = new FirefoxOptions();
                //options.AddArguments("--headless");
                //_driver = new FirefoxDriver(options);

                _driver.Navigate()
                    .GoToUrl(URI);

                _driver.SwitchTo().Frame("Hauptframe");

                var wait = new WebDriverWait(_driver, TimeSpan.FromSeconds(15));

                wait.Until(SeleniumExtras.WaitHelpers.ExpectedConditions.ElementIsVisible(By.Id("Inpuser")))
                    .SendKeys(username);
                wait.Until(SeleniumExtras.WaitHelpers.ExpectedConditions.ElementIsVisible(By.Id("Inppasswd")))
                    .SendKeys(password);
                _driver.FindElement(By.CssSelector("button.iflxButtonFactoryTextContainerOuter"))
                    .Click();

                _driver.SwitchTo().DefaultContent();
                _driver.SwitchTo().Frame(0);
                _driver.SwitchTo().Frame("Inhalt");

                wait.Until(SeleniumExtras.WaitHelpers.ExpectedConditions.ElementIsVisible(By.Id("ui-id-5")))
                    .Click();
                //Thread.Sleep(1000);
                //wait.Until(SeleniumExtras.WaitHelpers.ExpectedConditions.ElementIsVisible(By.Id("4")))
                //    .Click();

                _driver.SwitchTo().DefaultContent();
                _driver.SwitchTo().Frame(0);
                _driver.SwitchTo().Frame(_driver.FindElement(By.Name("Main")));


                wait.Until(SeleniumExtras.WaitHelpers.ExpectedConditions.ElementIsVisible(By.CssSelector("div[subitemid='4']")))
                    .Click();

                _driver.SwitchTo().Frame(wait.Until(SeleniumExtras.WaitHelpers.ExpectedConditions.ElementIsVisible(By.Name("mojou"))));
                _driver.SwitchTo().Frame(_driver.FindElement(By.Name("mojoudata")));

                wait.Until(SeleniumExtras.WaitHelpers.ExpectedConditions.ElementIsVisible(By.CssSelector("div#dataDiv > table > tbody > tr")));
                var tableRows = _driver.FindElements(By.CssSelector("div#dataDiv > table > tbody > tr"));
                var lenght = tableRows.Count;


                for (int i = 2; i <= lenght; i++)
                {
                    if (IsElementPresent(By.CssSelector($"div#dataDiv > table > tbody > tr:nth-child({i}) > td > table > tbody > tr > td:nth-child(3) > div > a > nobr")))
                    {
                        var tableDate = _driver.FindElement(By.CssSelector($"div#dataDiv > table > tbody > tr:nth-child({i}) > td > table > tbody > tr > td:nth-child(3) > div > a > nobr"));

                        var tableColumns = _driver.FindElements(By.CssSelector($"div#dataDiv > table > tbody > tr:nth-child({i}) > td"));
                        
                        var tableActualTime = _driver.FindElement(By.CssSelector($"div#dataDiv > table > tbody > tr:nth-child({i}) > td:nth-child({tableColumns.Count - 1})"));

                        int j = i;
                        var localAbsenceReasons = new List<AbsenceReason>();
                        do
                        {
                            if (!IsElementPresent(By.CssSelector($"div#dataDiv > table > tbody > tr:nth-child({j}) > td:nth-child({tableColumns.Count - 3})")))
                                break;
                            var tableAbsenceReasons = _driver.FindElement(By.CssSelector($"div#dataDiv > table > tbody > tr:nth-child({j}) > td:nth-child({tableColumns.Count - 3})"));                            

                            if (!tableAbsenceReasons.Text.IsEmptyOrAllSpaces())
                            {
                                if (tableAbsenceReasons.Text == "Kant.Feiertag")
                                    break;

                                var duration = GetAbsenceReasonTimeDuration(j, tableColumns.Count);
                                localAbsenceReasons.Add(new AbsenceReason()
                                { 
                                    Absence = tableAbsenceReasons.Text,
                                    Duration = duration
                                });
                            }

                            j++;
                        } while (!IsElementPresent(By.CssSelector($"div#dataDiv > table > tbody > tr:nth-child({j}) > td > table > tbody > tr > td:nth-child(3) > div > a > nobr")));

                        listActualTimes.Add(new DayAndTime()
                        {
                            ActualTime = tableActualTime.Text,
                            Day = tableDate.Text,
                            AbsenceReasons = localAbsenceReasons
                        });
                    }
                    else
                    {
                        continue;
                    }
                }
            }
            catch (Exception ex)
            {

            }
            finally
            {
                _driver.Close();
                _driver.Quit();
                _driver.Dispose();
            }

            return listActualTimes;
        }

        private static double? GetAbsenceReasonTimeDuration(int j, int tableColumnsCount)
        {
            var tableAbsenceReasonsStart = _driver.FindElement(By.CssSelector($"div#dataDiv > table > tbody > tr:nth-child({j}) > td:nth-child({tableColumnsCount - 6})"));
            var tableAbsenceReasonsEnd = _driver.FindElement(By.CssSelector($"div#dataDiv > table > tbody > tr:nth-child({j}) > td:nth-child({tableColumnsCount - 5})"));

            if (!tableAbsenceReasonsStart.Text.IsEmptyOrAllSpaces())
            {
                var absenceReasonsStart = tableAbsenceReasonsStart.Text;
                DateTime AbsenceReasonsStartDateTime = GetAbsenceTimeAsDatetime(absenceReasonsStart);
                var absenceReasonsEnd = tableAbsenceReasonsEnd.Text;
                DateTime AbsenceReasonsEndDateTime = GetAbsenceTimeAsDatetime(absenceReasonsEnd);

                var diff = AbsenceReasonsEndDateTime - AbsenceReasonsStartDateTime;
                return diff.TotalHours;
            }
            return null;
        }

        private static DateTime GetAbsenceTimeAsDatetime(string absenceReasonsStart)
        {
            var period = absenceReasonsStart.Substring(absenceReasonsStart.Length - 2);
            var absenceReasonsStartWithoutPeriod = absenceReasonsStart.Substring(0, absenceReasonsStart.Length - 2);
            DateTime absenceReasonsStartDatetime;
            if (period == "pm")
            {
                var AbsenceReasonsStartFirstPart = absenceReasonsStartWithoutPeriod.Split(":")[0];
                if (AbsenceReasonsStartFirstPart != "12")
                {
                    var AbsenceReasonsStartSecondPart = absenceReasonsStartWithoutPeriod.Split(":")[1];
                    Int32.TryParse(AbsenceReasonsStartFirstPart, out var number);
                    var hoursAfterAddition = number + 12;
                    absenceReasonsStartWithoutPeriod = hoursAfterAddition.ToString() + ":" + AbsenceReasonsStartSecondPart;
                    _ = DateTime.TryParse(absenceReasonsStartWithoutPeriod, out absenceReasonsStartDatetime);
                    return absenceReasonsStartDatetime;
                }
                _ = DateTime.TryParse(absenceReasonsStartWithoutPeriod, out absenceReasonsStartDatetime);
                return absenceReasonsStartDatetime;
            }
            DateTime.TryParse(absenceReasonsStartWithoutPeriod, out absenceReasonsStartDatetime);
            return absenceReasonsStartDatetime;
        }

        private static bool IsElementPresent(By by)
        {
            try
            {
                _driver.FindElement(by);
                return true;
            }
            catch (NoSuchElementException)
            {
                return false;
            }
        }        
    }
}
