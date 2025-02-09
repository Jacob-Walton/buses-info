using System.Collections.Generic;

namespace BusInfo.Models.Accounts
{
    public class UserPreferences
    {
        public List<string> PreferredRoutes { get; set; } = [];
        public bool ShowPreferredRoutesFirst { get; set; }
        public bool EnableEmailNotifications { get; set; }
    }
}
