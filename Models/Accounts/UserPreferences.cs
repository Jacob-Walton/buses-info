using System.Collections.Generic;

namespace BusInfo.Models.Accounts
{
    public class UserPreferences
    {
        private List<string> _preferredRoutes = [];
        public ICollection<string> PreferredRoutes 
        {
            get => _preferredRoutes;
            init => _preferredRoutes = [..value];
        }
        public bool ShowPreferredRoutesFirst { get; set; }
        public bool EnableEmailNotifications { get; set; }
    }
}
