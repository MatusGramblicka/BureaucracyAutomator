using BureaucracyAutomator2.Contracts;
using Microsoft.Exchange.WebServices.Data;
using System;
using System.Collections.Generic;

namespace BureaucracyAutomator2
{
    public static class ExchangeLib
    {
        private static ExchangeService InitializeExchangeService(string userName, string password)
        {
            // https://social.msdn.microsoft.com/Forums/en-US/8171b184-599b-4417-83ad-616a1e047ff9/is-there-a-way-to-getsend-email-using-exchange-webservice-without-using-autodiscover?forum=exchangesvrdevelopment
            ExchangeService service = new ExchangeService(ExchangeVersion.Exchange2013_SP1);
            service.EnableScpLookup = false;
            service.Credentials = new WebCredentials(userName, password, "int");
            service.TraceEnabled = true;
            service.TraceFlags = TraceFlags.All;
            service.Url = new Uri("https://cas.kistler.com/ews/exchange.asmx");

            return service;
        }

        public static (List<Exchange>, bool) GetExchangeData(string userName, string password)
        {
            CalendarFolder calendar = null;
            List<Exchange> exchangeData = new List<Exchange>();

            try
            {
                // Initialize the calendar folder object with only the folder ID. 
                calendar = CalendarFolder.Bind(InitializeExchangeService(userName, password)/*service*/, WellKnownFolderName.Calendar, new PropertySet());
            }
            catch (ServiceRequestException)
            {
                return (exchangeData, false);
            }

            // https://docs.microsoft.com/en-us/previous-versions/office/developer/exchange-server-2010/dn439786(v=exchg.80)
            // Initialize values for the start and end times, and the number of appointments to retrieve.  
            var today = DateTime.Now;
            DateTime startDate = new DateTime(today.Year, today.Month, 1);
            DateTime endDate = startDate.AddDays(DateTime.DaysInMonth(today.Year, today.Month));
            const int NUM_APPTS = 300;

            // Set the start and end time and number of appointments to retrieve.
            CalendarView cView = new CalendarView(startDate, endDate, NUM_APPTS);

            // Limit the properties returned to the appointment's subject, start time, and end time.
            cView.PropertySet = new PropertySet(AppointmentSchema.Subject, AppointmentSchema.Start, AppointmentSchema.End, AppointmentSchema.Duration);

            // Retrieve a collection of appointments by using the calendar view.
            FindItemsResults<Appointment> appointments = calendar.FindAppointments(cView);

            foreach (Appointment appointment in appointments)
            {
                exchangeData.Add(new Exchange
                {
                    Subject = appointment.Subject?.ToString(),
                    Start = appointment.Start,
                    Duration = appointment.Duration
                });
            }

            return (exchangeData, true);
        }

        public static bool TestConnection(string userName, string password)
        {
            try
            {
                CalendarFolder calendar = CalendarFolder.Bind(InitializeExchangeService(userName, password), WellKnownFolderName.Calendar, new PropertySet());
            }
            catch (ServiceRequestException)
            {
                return false;
            }

            return true;
        }       
    }
}
