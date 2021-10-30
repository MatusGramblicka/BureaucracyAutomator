using System.Collections.Generic;

namespace BureaucracyAutomator2.Contracts
{
    public class DayAndTime
    {
        public string Day { get; set; }
        public string ActualTime { get; set; }
        public List<AbsenceReason> AbsenceReasons { get; set; }
    }
}
