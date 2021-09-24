using System;
using System.Windows;
using System.Windows.Input;
using System.Management;
using System.Configuration;
using System.IO;
using System.Threading;
using System.Speech.Synthesis;

namespace SafeBattery
{
    /// <summary>
    /// Logique d'interaction pour MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        private Point lmAbs = new Point();
        System.Timers.Timer timer = new System.Timers.Timer();
        System.Timers.Timer tooltip_timer = new System.Timers.Timer();
        private System.Windows.Forms.NotifyIcon MyNotifyIcon;
        private string pathIco = Directory.GetCurrentDirectory() + "/Images/battery.ico";
        private bool canSendNotif = false;
        private string notifInter = ConfigurationManager.AppSettings["NotifInterval"];
        private int LastRappelMarge = 10;
        private SpeechSynthesizer speech = new SpeechSynthesizer();

        public MainWindow()
        {
            InitializeComponent();
            MyNotifyIcon = new System.Windows.Forms.NotifyIcon();
            MyNotifyIcon.ContextMenu = new System.Windows.Forms.ContextMenu();
            System.Windows.Forms.MenuItem itemStatut = new System.Windows.Forms.MenuItem("Voir le Statut", onShowStatut);
            System.Windows.Forms.MenuItem itemClose = new System.Windows.Forms.MenuItem("Fermer", onCloseEvent);
            MyNotifyIcon.ContextMenu.MenuItems.Add(itemStatut);
            MyNotifyIcon.ContextMenu.MenuItems.Add(itemClose);
            MyNotifyIcon.Icon = new System.Drawing.Icon(pathIco);
            MyNotifyIcon.MouseDoubleClick += new System.Windows.Forms.MouseEventHandler(MyNotifyIcon_MouseDoubleClick);
        }

        private void onShowStatut(object sender, EventArgs e)
        {
            this.Show();
            this.WindowState = WindowState.Normal;
        }

        private void onCloseEvent(object sender, EventArgs e)
        {
            if(MessageBox.Show("Voulez vous vraiment arrêter SafeBattey !!?","Arrêt SafeBattery",MessageBoxButton.YesNo,MessageBoxImage.Question)
                == MessageBoxResult.Yes)
            {
                this.Close();
            }
        }

        private void MyNotifyIcon_MouseDoubleClick(object sender, System.Windows.Forms.MouseEventArgs e)
        {
            this.Show();
            this.WindowState = WindowState.Normal;
        }

        private void Window_StateChanged(object sender, EventArgs e)
        {
            if (this.WindowState == WindowState.Minimized)
            {
                this.ShowInTaskbar = false;
                MyNotifyIcon.BalloonTipTitle = "Minimize Sucessful";
                MyNotifyIcon.BalloonTipText = "Minimized the app ";
                MyNotifyIcon.ShowBalloonTip(400);
                MyNotifyIcon.Visible = true;
                this.Hide();
            }
            else if (this.WindowState == WindowState.Normal)
            {
                MyNotifyIcon.Visible = false;
                this.ShowInTaskbar = true;
            }
        }

        void PnMouseDown(object sender, System.Windows.Input.MouseEventArgs e)
        {
            this.lmAbs = e.GetPosition(this);
            this.lmAbs.Y = Convert.ToInt16(this.Top) + this.lmAbs.Y;
            this.lmAbs.X = Convert.ToInt16(this.Left) + this.lmAbs.X;
        }

        void PnMouseMove(object sender, System.Windows.Input.MouseEventArgs e)
        {
            if (e.LeftButton == MouseButtonState.Pressed)
            {
                Point MousePosition = e.GetPosition(this);
                Point MousePositionAbs = new Point();
                MousePositionAbs.X = Convert.ToInt16(this.Left) + MousePosition.X;
                MousePositionAbs.Y = Convert.ToInt16(this.Top) + MousePosition.Y;
                this.Left = this.Left + (MousePositionAbs.X - this.lmAbs.X);
                this.Top = this.Top + (MousePositionAbs.Y - this.lmAbs.Y);
                this.lmAbs = MousePositionAbs;
            }
        }

        private void btnClose_Click(object sender, RoutedEventArgs e)
        {
            this.WindowState = WindowState.Minimized;
        }

        private void btnMinimise_Click(object sender, RoutedEventArgs e)
        {
            this.WindowState = WindowState.Minimized;
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            // timer for battery statut handle
            timer.Start();
            timer.Elapsed += Timer_Elapsed;
            timer.AutoReset = true;
            timer.Enabled = true;

            // timer for for notification handle 
            tooltip_timer.Start();
            tooltip_timer.Enabled = true;
            tooltip_timer.Elapsed += Tooltip_timer_Elapsed;
            tooltip_timer.AutoReset = true;
            int NotifTimeInt = 30;
            if(Int32.TryParse(notifInter, out NotifTimeInt))
            {
                tooltip_timer.Interval = NotifTimeInt * 1000;
            }
            else
            {
                tooltip_timer.Interval = 30 * 1000;
            }
        }

        private void Tooltip_timer_Elapsed(object sender, System.Timers.ElapsedEventArgs e)
        {
            Dispatcher.BeginInvoke((Action)delegate ()
            {
                canSendNotif = true;
            });
        }

        private void Timer_Elapsed(object sender, System.Timers.ElapsedEventArgs e)
        {
            try
            {
                System.Management.ObjectQuery query = new ObjectQuery("Select * FROM Win32_Battery");
                ManagementObjectSearcher searcher = new ManagementObjectSearcher(query);
                ManagementObjectCollection collection = searcher.Get();
                int BatteryStatus = 0;
                int BatteryLevel = 0;

                foreach (ManagementObject mo in collection)
                {
                    foreach (PropertyData property in mo.Properties)
                    {
                        if (property.Name.ToLower() == "BatteryStatus".ToLower())
                        {
                            BatteryStatus = Int32.Parse(property.Value.ToString());
                        }

                        if (property.Name.ToLower() == "EstimatedChargeRemaining".ToLower())
                        {
                            BatteryLevel = Int32.Parse(property.Value.ToString());
                        }
                    }
                }

                Dispatcher.BeginInvoke((Action)delegate ()
                {
                    if (BatteryStatus == 1)
                    {
                        #region CAS BATTERIE AUTONOME
                        StatutBattery.Text = "En Mode : Autonomie";
                        int MinLevel = 0,MaxLevel = 0;

                        if (Int32.TryParse(ConfigurationManager.AppSettings.Get("MinDeChargeLevel"),out MinLevel)
                            && Int32.TryParse(ConfigurationManager.AppSettings.Get("MaxChargeLevel"),out MaxLevel))
                        {
                            if(BatteryLevel <= MinLevel)
                            {
                                AdviseText.Foreground = System.Windows.Media.Brushes.Red;
                                AdviseText.Text = " \" Mettre en Charge \"";

                                if (canSendNotif)
                                {
                                    canSendNotif = false;
                                    MyNotifyIcon.ShowBalloonTip(2000, "Veuillez Mettre l'Ordinateur Sous Tension !!!"
                                                                    , "Brancher le Chargeur", System.Windows.Forms.ToolTipIcon.Warning);
                                    tooltip_timer.Stop();
                                    tooltip_timer.Start();

                                    if (BatteryLevel == (MinLevel - (LastRappelMarge - 2)))
                                    {
                                        // Envoi d'un speechText
                                        var speechText = "Veuillez Mettre le PC sous tension. Pour éviter des problèmes d'autonomie de la Batterie, Votre PC sera mise en veille prolongée, dans quelques instants !!";                  
                                        speech.Speak(speechText);
                                    }

                                    if (BatteryLevel <= (MinLevel - LastRappelMarge))
                                    {
                                        // Démande de Mise en veille 
                                        System.Windows.Forms.Application.SetSuspendState(System.Windows.Forms.PowerState.Hibernate, true, false);
                                    }
                                }
                            }
                            else
                            {
                                if(BatteryLevel > MaxLevel)
                                {
                                    AdviseText.Foreground = System.Windows.Media.Brushes.Orange;
                                    AdviseText.Text = " \" Batterie En Réfroidissement \"";
                                }
                                else
                                {
                                    AdviseText.Foreground = System.Windows.Media.Brushes.LightGreen;
                                    AdviseText.Text = " \" En Safe Zone \"";
                                }
                            }
                        }
                        ArcIndicator.EndAngle = (BatteryLevel * 360) / 100;
                        NiveauBattery.Text = BatteryLevel + "%";
                        #endregion
                    }
                    else if (BatteryStatus == 2)
                    {
                        #region CAS BATTERIE EN CHARGE
                        StatutBattery.Text = "En Mode : Charge"; 
                        int MaxLevel = 0, MinLevel = 0;

                        if (Int32.TryParse(ConfigurationManager.AppSettings.Get("MaxChargeLevel"), out MaxLevel)
                            && Int32.TryParse(ConfigurationManager.AppSettings.Get("MinDeChargeLevel"), out MinLevel))
                        {
                            if (BatteryLevel >= MaxLevel)
                            {
                                AdviseText.Foreground = System.Windows.Media.Brushes.Red;
                                AdviseText.Text = " \" Débrancher le Chargeur \"";

                                if(canSendNotif)
                                {
                                    canSendNotif = false;
                                    MyNotifyIcon.ShowBalloonTip(2000, "Veuillez Débranchez le Chargeur Pour éviter la surchauffe !!!"
                                                                    , "Débrancher le Chargeur",System.Windows.Forms.ToolTipIcon.Warning);
                                    tooltip_timer.Stop();
                                    tooltip_timer.Start();

                                    if (BatteryLevel == (MaxLevel + (LastRappelMarge - 2)))
                                    {
                                        // Envoi d'un speechText
                                        var speechText = "Veuillez Débranchez la Batterie du Secteur. Afin d'éviter la surchauffe, Le PC sera mise en veille, dans quelques instants !!";
                                        speech.SpeakAsync(speechText);
                                    }

                                    if (BatteryLevel >= (MaxLevel + LastRappelMarge))
                                    {
                                        // Démande de Mise en veille 
                                        System.Windows.Forms.Application.SetSuspendState(System.Windows.Forms.PowerState.Suspend, true, false);
                                    }
                                }
                            }
                            else
                            {
                                if (BatteryLevel < MinLevel)
                                {
                                    AdviseText.Foreground = System.Windows.Media.Brushes.Orange;
                                    AdviseText.Text = " \" En Charge de Rétablissement \"";
                                }
                                else
                                {
                                    AdviseText.Foreground = System.Windows.Media.Brushes.LightGreen;
                                    AdviseText.Text = " \" En Safe Zone \"";
                                }
                            }
                        }
                        ArcIndicator.EndAngle = (BatteryLevel * 360) / 100;
                        NiveauBattery.Text = BatteryLevel + "%";
                        #endregion
                    }
                });
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);
            }
        }
    }
}
