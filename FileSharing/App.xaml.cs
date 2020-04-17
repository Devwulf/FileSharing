using System;
using System.Collections.Generic;
using System.Linq;
using Xamarin.Essentials;
using Xamarin.Forms;
using Xamarin.Forms.Xaml;

namespace FileSharing
{
    public partial class App : Application
    {
        private MainPage mainPage;
        public App()
        {
            InitializeComponent();

            mainPage = new MainPage();
            Connectivity.ConnectivityChanged += Connectivity_ConnectivityChanged;
            ChangeViewOnConnectivity(Connectivity.NetworkAccess, Connectivity.ConnectionProfiles);
        }

        private void ChangeViewOnConnectivity(NetworkAccess access, IEnumerable<ConnectionProfile> profiles)
        {
            if (access == NetworkAccess.None ||
                access == NetworkAccess.Unknown ||
                !profiles.Contains(ConnectionProfile.WiFi))
            {
                // No wifi
                Label label = new Label()
                {
                    Text = "Not connected to WiFi",
                    VerticalOptions = LayoutOptions.CenterAndExpand,
                    HorizontalOptions = LayoutOptions.CenterAndExpand
                };
                StackLayout stack = new StackLayout() { Children = { label } };

                MainPage = new ContentPage() { Content = stack };
            }
            else
            {
                MainPage = mainPage;
                mainPage.Initialize();
            }
        }

        private void Connectivity_ConnectivityChanged(object sender, ConnectivityChangedEventArgs e)
        {
            ChangeViewOnConnectivity(e.NetworkAccess, e.ConnectionProfiles);
        }

        protected override void OnStart()
        {
        }

        protected override void OnSleep()
        {
        }

        protected override void OnResume()
        {
        }
    }
}
