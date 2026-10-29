using System.Configuration;

namespace BookClubApp.Properties {
    internal sealed partial class Settings {
        [UserScopedSetting]
        [DefaultSettingValue("")]
        public string RememberedPhone {
            get { return (string)this["RememberedPhone"]; }
            set { this["RememberedPhone"] = value; }
        }
    }
}
