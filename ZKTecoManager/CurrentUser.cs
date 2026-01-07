using System.Collections.Generic;

namespace ZKTecoManager
{
    public static class CurrentUser
    {
        public static int UserId { get; set; }
        public static string Name { get; set; }
        public static string Role { get; set; }
        public static string BadgeNumber { get; set; }

        // ✅ NEW: Permission for manual time editing
        public static bool CanEditTimes { get; set; }

        // System access type: "full_access" or "leave_only"
        public static string SystemAccessType { get; set; }

        // Use Lists for easy adding during login
        public static List<int> PermittedDepartmentIds { get; set; } = new List<int>();
        public static List<int> PermittedDeviceIds { get; set; } = new List<int>();

        public static void SetUser(User user)
        {
            UserId = user.UserId;
            Name = user.Name;
            Role = user.Role;
            BadgeNumber = user.BadgeNumber;

            // Clear previous permissions
            PermittedDepartmentIds.Clear();
            PermittedDeviceIds.Clear();
        }

        // ✅ NEW: Overload to set user with permissions
        public static void SetUser(User user, bool canEditTimes)
        {
            SetUser(user);
            CanEditTimes = canEditTimes;
        }

        public static void Clear()
        {
            UserId = 0;
            Name = null;
            Role = null;
            BadgeNumber = null;
            CanEditTimes = false; // ✅ Reset permission
            SystemAccessType = null; // Reset system access type
            PermittedDepartmentIds.Clear();
            PermittedDeviceIds.Clear();
        }
    }
}
