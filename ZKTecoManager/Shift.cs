using System;
using System.Collections.Generic;

namespace ZKTecoManager
{
    public class Shift
    {
        public int ShiftId { get; set; }
        public string ShiftName { get; set; }

        // OLD PROPERTIES (can be removed or ignored)
        public TimeSpan StartTime { get; set; }
        public TimeSpan EndTime { get; set; }

        // NEW: A list to hold all the specific punch times for this shift
        public List<TimeSpan> Rules { get; set; }

        public Shift()
        {
            Rules = new List<TimeSpan>();
        }
    }
}

