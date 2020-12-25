﻿using System;
using System.Windows.Threading;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using System.Diagnostics;
using System.Drawing;
using System.Windows.Forms;
using System.IO;
using PACTCore;
using System.Runtime.InteropServices;

namespace PACTWPF
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        private static PACTInstance pact;
        private static DispatcherTimer PerformanceStatisticsUpdateTimer;

        private NotifyIcon TrayIcon;
        private List<ThreadUtilizationBar> ThreadBars { get; set; }
        private PerformanceCounter TotalCPUUsage;

        private readonly CollectionViewSource normalViewSource = new CollectionViewSource();
        private readonly CollectionViewSource hpViewSource = new CollectionViewSource();
        private readonly CollectionViewSource customViewSource = new CollectionViewSource();
        private readonly CollectionViewSource blacklistViewSource = new CollectionViewSource();




        public MainWindow()
        {
            pact = new PACTInstance();
            pact.ToggleProcessOverwatch();
            ThreadBars = new List<ThreadUtilizationBar>();
            TotalCPUUsage = new PerformanceCounter("Processor", "% Processor Time", "_Total");
            InitializeComponent();
            InitializePerformanceStatisticsUpdateTimer();
            InitTrayIcon();
            pact.ConfigUpdated += UpdatePerformanceBarColors;
            UpdatePerformanceBarColors(this, EventArgs.Empty);
        }

        public void InitTrayIcon()
        {
            TrayIcon = new NotifyIcon();
            TrayIcon.Icon = new Icon("tray.ico");
            TrayIcon.Text = "Click to bring PACT back.";
            TrayIcon.Visible = true;

            TrayIcon.BalloonTipIcon = new ToolTipIcon();
            TrayIcon.BalloonTipTitle = "PACT for Windows";
            TrayIcon.BalloonTipText = "PACT is minimized to tray.";

            TrayIcon.Click += TrayIcon_Clicked;
        }

        public void TrayIcon_Clicked(object sender, EventArgs args)
        {
            this.Show();
            this.WindowState = WindowState.Normal;
        }

        private void Window_StateChanged(object sender, EventArgs e)
        {
            if (this.WindowState == WindowState.Minimized)
            {
                PerformanceStatisticsUpdateTimer.Stop();
                TrayIcon.ShowBalloonTip(5000);
                this.Hide();
            }
            else
            {
                PerformanceStatisticsUpdateTimer.Start();
                this.Show();
            }
        }

        private void Window_Closed(object sender, EventArgs e)
        {
            TrayIcon.Dispose();
        }

        private void Grid_Status_CPU_Initialized(object sender, EventArgs e)
        {
            int threadCount = Environment.ProcessorCount;

            int columns = 2;
            int rows = 1;
            int gridSize = columns * rows;

            while (gridSize < threadCount)
            {
                if (rows <= columns)
                {
                    rows++;
                }
                else
                {
                    columns += 2;
                }

                gridSize = columns * rows;
            }

            for (int i = 0; i < columns; i++)
            {
                Grid_Status_CPU.ColumnDefinitions.Add(new ColumnDefinition());
            }

            for (int i = 0; i < rows; i++)
            {
                Grid_Status_CPU.RowDefinitions.Add(new RowDefinition());
            }

            int assigned = 0;
            for (int i = 0; i < rows; i++)
            {
                for (int j = 0; j < columns; j++)
                {
                    if (assigned > threadCount)
                    {
                        break;
                    }

                    ThreadUtilizationBar tub = new ThreadUtilizationBar(assigned);
                    Grid.SetRow(tub, i);
                    Grid.SetColumn(tub, j);
                    ThreadBars.Add(tub);

                    Grid.SetRow(tub.CustomLabel, i);
                    Grid.SetColumn(tub.CustomLabel, j);

                    assigned++;

                    Grid_Status_CPU.Children.Add(tub);
                    Grid_Status_CPU.Children.Add(tub.CustomLabel);
                }
            }
        }

        public void InitializePerformanceStatisticsUpdateTimer()
        {
            PerformanceStatisticsUpdateTimer = new System.Windows.Threading.DispatcherTimer(DispatcherPriority.Render);
            PerformanceStatisticsUpdateTimer.Interval = new TimeSpan(0, 0, 0, 0, 1000);
            PerformanceStatisticsUpdateTimer.Tick += new EventHandler(UpdatePerformanceStatistics);
            PerformanceStatisticsUpdateTimer.Start();
        }

        private void UpdatePerformanceBarColors(object source, EventArgs e)
        {
            var highs = pact.GetHighPerformanceCores();
            var normals = pact.GetNormalPerformanceCores();
            for (int i = 0; i < ThreadBars.Count; i++)
            {
                var bar = ThreadBars[i];
                bar.AutoSetColor(normals.Contains(i), highs.Contains(i));
            }
        }

        private void UpdatePerformanceStatistics(Object source, EventArgs e)
        {
            // Update CPU Utilization Values
            foreach (var bar in ThreadBars)
            {
                bar.UpdateUtilization();
            }

            Total_CPU_Usage_Label.Content = $"{TotalCPUUsage.NextValue().ToString("0")}%";

            var allRunningProcesses = pact.GetAllRunningProcesses().Select(x => x.ToLower()).ToList();

            var HighPerformanceProcesses = allRunningProcesses.Intersect(pact.GetHighPerformanceProcesses().Select(x => x.ToLower())).Count();
            var exceptionPriorityProcesses = allRunningProcesses.Intersect(pact.GetCustomProcesses().Select(x => x.ToLower())).Count();
            var inaccessibleProcesses = pact.GetProtectedProcesses().Count();

            Total_Process_Count_Label.Content = allRunningProcesses.Count;
            Active_High_Performance_Process_Count_Label.Content = HighPerformanceProcesses;
            Active_Custom_Count_Label.Content = exceptionPriorityProcesses;
            Inaccessible_Process_Count_Label.Content = inaccessibleProcesses;
        }

        private void Button_Toggle_Click(object sender, RoutedEventArgs e)
        {
            if (pact.ToggleProcessOverwatch())
            {
                Label_ToggleStatus.Content = "PACT is ACTIVE";
            }
            else
            {
                Label_ToggleStatus.Content = "PACT is PAUSED";
            }
        }

        ////////////////////////////////////////////////////////////////////////////
        ////////////////////////////////////////////////////////////////////////////
        ////                           Configure Tab                            ////
        ////////////////////////////////////////////////////////////////////////////
        ////////////////////////////////////////////////////////////////////////////

        bool ProcessSearchFilter(object obj)
        {
            if (Configure_Search.Text != null && Configure_Search.Text != "")
            {
                return (obj as string).ToLower().StartsWith(Configure_Search.Text.ToLower());
            }
            else
            {
                return true;
            }
        }

        private void Configure_Search_TextChanged(object sender, TextChangedEventArgs e)
        {
            TriggerListUpdate();

        }

        private void Button_Refresh_Click(object sender, RoutedEventArgs e)
        {
            TriggerListUpdate();
        }

        private void TriggerListUpdate()
        {
            ListView_Normal_Initialized(this, null);
            ListView_HighPerformance_Initialized(this, null);
            ListView_Custom_Initialized(this, null);
            ListView_Blacklist_Initialized(this, null);

            ListView_Normal_Validate();
            ListView_HighPerformance_Validate();
            ListView_Custom_Validate();
            ListView_Blacklist_Validate();
        }

        ////////////////////////////////////////////////////////////////////////////
        //                     Normal Performance Column                          //
        ////////////////////////////////////////////////////////////////////////////

        private void ListView_Normal_Initialized(object sender, EventArgs e)
        {
            ListView_Normal.Items.Clear();
            foreach (var hpp in pact.GetNormalPerformanceProcesses())
            {
                ListView_Normal.Items.Add(hpp);
            }

            ListView_Normal.Items.Filter = ProcessSearchFilter;
        }

        private void ListView_Normal_SelectionChanged(object sender, EventArgs e)
        {
            Button_Normal_MoveToBlacklist.IsEnabled = true;
            Button_Normal_MoveToHighPerformance.IsEnabled = true;
            Button_Normal_MoveToCustom.IsEnabled = true;
        }

        private void Button_Normal_MoveToBlacklist_Click(object sender, RoutedEventArgs e)
        {
            pact.AddToBlacklist(ListView_Normal.SelectedItem.ToString());
            TriggerListUpdate();
        }

        private void Button_Normal_Configure_Click(object sender, RoutedEventArgs e)
        {
            ProcessConfigEditWindow window = new ProcessConfigEditWindow();
            window.TargetProcessOrGroup = "[Normal Priority Processes]";
            ProcessConfig conf;
            if (window.ShowDialog() == true)
            {
                conf = window.GenerateConfig();
                pact.UpdateDefaultPriorityProcessConfig(conf);
            }

            TriggerListUpdate();
        }

        private void Button_Normal_MoveToCustom_Click(object sender, RoutedEventArgs e)
        {
            ProcessConfigEditWindow window = new ProcessConfigEditWindow();
            window.TargetProcessOrGroup = ListView_Normal.SelectedItem.ToString();
            ProcessConfig conf;
            if (window.ShowDialog() == true)
            {
                conf = window.GenerateConfig();
                pact.AddToCustomPriority(window.TargetProcessOrGroup, conf);
            }

            TriggerListUpdate();
        }

        private void Button_Normal_MoveToHighPerformance_Click(object sender, RoutedEventArgs e)
        {
            pact.AddToHighPerformance(ListView_Normal.SelectedItem.ToString());
            TriggerListUpdate();
        }

        private void ListView_Normal_Validate()
        {
            if (ListView_Normal.SelectedItem == null)
            {
                Button_Normal_MoveToBlacklist.IsEnabled = false;
                Button_Normal_MoveToHighPerformance.IsEnabled = false;
                Button_Normal_MoveToCustom.IsEnabled = false;
            }
        }

        ////////////////////////////////////////////////////////////////////////////
        //                       High Performance Column                          //
        ////////////////////////////////////////////////////////////////////////////
        
        private void ListView_HighPerformance_Initialized(object sender, EventArgs e)
        {
            ListView_HighPerformance.Items.Clear();
            foreach (var hpp in pact.GetHighPerformanceProcesses())
            {
                ListView_HighPerformance.Items.Add(hpp);
            }

            ListView_HighPerformance.Items.Filter = ProcessSearchFilter;
        }

        private void ListView_HighPerformance_SelectionChanged(object sender, EventArgs e)
        {
            Button_HighPerformance_Remove.IsEnabled = true;
            Button_HighPerformance_MoveToCustom.IsEnabled = true;
        }

        private void Button_HighPerformance_Configure_Click(object sender, RoutedEventArgs e)
        {
            ProcessConfigEditWindow window = new ProcessConfigEditWindow();
            window.TargetProcessOrGroup = "[High Priority Processes]";
            ProcessConfig conf;
            if (window.ShowDialog() == true)
            {
                conf = window.GenerateConfig();
                pact.UpdateHighPerformanceProcessConfig(conf);
            }
        }

        private void Button_HighPerformance_MoveToCustom_Click(object sender, RoutedEventArgs e)
        {
            ProcessConfigEditWindow window = new ProcessConfigEditWindow();
            window.TargetProcessOrGroup = ListView_HighPerformance.SelectedItem.ToString();
            ProcessConfig conf;
            if (window.ShowDialog() == true)
            {
                conf = window.GenerateConfig();
                pact.AddToCustomPriority(window.TargetProcessOrGroup, conf);
            }
            TriggerListUpdate();
        }

        private void Button_HighPerformance_AddManual_Click(object sender, RoutedEventArgs e)
        {
            ProcessNameEntryWindow window = new ProcessNameEntryWindow();
            if (window.ShowDialog() == true)
            {
                pact.AddToHighPerformance(window.ProcessName);
            }
            TriggerListUpdate();
        }

        private void Button_HighPerformance_Remove_Click(object sender, RoutedEventArgs e)
        {
            pact.ClearProcess(ListView_HighPerformance.SelectedItem.ToString());
            TriggerListUpdate();
        }

        private void ListView_HighPerformance_Validate()
        {
            if (ListView_HighPerformance.SelectedItem == null)
            {
                Button_HighPerformance_MoveToCustom.IsEnabled = false;
                Button_HighPerformance_Remove.IsEnabled = false;
            }
        }

        ////////////////////////////////////////////////////////////////////////////
        //                       Custom Performance Column                        //
        ////////////////////////////////////////////////////////////////////////////

        private void ListView_Custom_Initialized(object sender, EventArgs e)
        {
            ListView_Custom.Items.Clear();
            foreach (var hpp in pact.GetCustomProcesses())
            {
                ListView_Custom.Items.Add(hpp);
            }

            ListView_Custom.Items.Filter = ProcessSearchFilter;
        }

        private void ListView_Custom_SelectionChanged(object sender, EventArgs e)
        {
            Button_Custom_Configure.IsEnabled = true;
            Button_Custom_MoveToHighPerformance.IsEnabled = true;
            Button_Custom_Remove.IsEnabled = true;
        }

        private void Button_Custom_Configure_Click(object sender, RoutedEventArgs e)
        {
            ProcessConfigEditWindow window = new ProcessConfigEditWindow();
            window.TargetProcessOrGroup = ListView_Custom.SelectedItem.ToString();
            ProcessConfig conf;
            if (window.ShowDialog() == true)
            {
                conf = window.GenerateConfig();
                pact.AddToCustomPriority(ListView_Custom.SelectedItem.ToString(), conf);
            }
            TriggerListUpdate();
        }

        private void Button_Custom_MoveToHighPerformance_Click(object sender, RoutedEventArgs e)
        {
            pact.AddToHighPerformance(ListView_Custom.SelectedItem.ToString());
            TriggerListUpdate();
        }

        private void Button_Custom_Remove_Click(object sender, RoutedEventArgs e)
        {
            pact.ClearProcess(ListView_Custom.SelectedItem.ToString());
            TriggerListUpdate();
        }

        private void ListView_Custom_Validate()
        {
            if (ListView_Custom.SelectedItem == null)
            {
                Button_Custom_Configure.IsEnabled = false;
                Button_Custom_MoveToHighPerformance.IsEnabled = false;
                Button_Custom_Remove.IsEnabled = false;
            }
        }

        ////////////////////////////////////////////////////////////////////////////
        //                           Blacklist Column                             //
        ////////////////////////////////////////////////////////////////////////////

        private void ListView_Blacklist_Initialized(object sender, EventArgs e)
        {
            ListView_Blacklist.Items.Clear();
            foreach (var hpp in pact.GetBlacklistedProcesses())
            {
                ListView_Blacklist.Items.Add(hpp);
            }

            ListView_Blacklist.Items.Filter = ProcessSearchFilter;
        }

        private void ListView_Blacklist_SelectionChanged(object sender, EventArgs e)
        {
            Button_Blacklist_Remove.IsEnabled = true;
        }

        private void Button_Blacklist_Add_Click(object sender, RoutedEventArgs e)
        {
            ProcessNameEntryWindow window = new ProcessNameEntryWindow();
            if (window.ShowDialog() == true)
            {
                pact.AddToBlacklist(window.ProcessName);
            }
            TriggerListUpdate();
        }

        private void Button_Blacklist_Remove_Click(object sender, RoutedEventArgs e)
        {
            pact.ClearProcess(ListView_Blacklist.SelectedItem.ToString());
            TriggerListUpdate();
        }

        private void ListView_Blacklist_Validate()
        {
            if (ListView_Blacklist.SelectedItem == null)
            {
                Button_Blacklist_Remove.IsEnabled = false;
            }
        }

        ////////////////////////////////////////////////////////////////////////////
        //                          Settings/Options Tab                          //
        ////////////////////////////////////////////////////////////////////////////

        private string OpenFile(string defaultExt, string filter)
        {
            OpenFileDialog dialog = new OpenFileDialog();

            dialog.DefaultExt = defaultExt;
            dialog.Filter = filter;
            dialog.RestoreDirectory = true;

            if (dialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
            {
                return dialog.FileName;
            }

            return "";
        }

        private string SaveFile(string defaultExt, string filter)
        {
            SaveFileDialog dialog = new SaveFileDialog();

            dialog.DefaultExt = defaultExt;
            dialog.Filter = filter;
            dialog.RestoreDirectory = true;

            if (dialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
            {
                return dialog.FileName;
            }

            return "";
        }

        private void Button_Options_HighPriority_Import_Click(object sender, RoutedEventArgs e)
        {
            string path = OpenFile(".txt", "Text Files (*.txt)|*.txt");
            if (path != "")
            {
                pact.ImportHighPerformance(path);
            }
            TriggerListUpdate();
        }

        private void Button_Options_HighPriority_Export_Click(object sender, RoutedEventArgs e)
        {
            string path = SaveFile(".txt", "Text Files (*.txt)|*.txt");
            if (path != "")
            {
                pact.ExportHighPerformance(path);
            }
        }

        private void Button_Options_HighPriority_Clear_Click(object sender, RoutedEventArgs e)
        {
            pact.ClearHighPerformance();
            TriggerListUpdate();
        }

        private void Button_Options_Blacklist_Import_Click(object sender, RoutedEventArgs e)
        {
            string path = OpenFile(".txt", "Text Files (*.txt)|*.txt");
            if (path != "")
            {
                pact.ImportBlacklist(path);
            }
            TriggerListUpdate();
        }

        private void Button_Options_Blacklist_Export_Click(object sender, RoutedEventArgs e)
        {
            string path = SaveFile(".txt", "Text Files (*.txt)|*.txt");
            if (path != "")
            {
                pact.ExportBlacklist(path);
            }
        }

        private void Button_Options_Blacklist_Clear_Click(object sender, RoutedEventArgs e)
        {
            pact.ClearBlackList();
            TriggerListUpdate();
        }

        private void Button_Options_Config_Import_Click(object sender, RoutedEventArgs e)
        {
            string path = OpenFile(".json", "JSON Files (*.json)|*.json");
            if (path != "")
            {
                pact.ImportConfig(path);
            }
            TriggerListUpdate();
            // I don't understand why (yet), but for some reason
            // the performance bars do not update if Reset
            // or Import config buttons are pressed.
            // This harmless hack ensures they update properly. 
            pact.ToggleProcessOverwatch();
            pact.ToggleProcessOverwatch();
        }

        private void Button_Options_Config_Export_Click(object sender, RoutedEventArgs e)
        {
            string path = SaveFile(".json", "JSON Files (*.json)|*.json");
            if (path != "")
            {
                pact.ExportConfig(path);
            }
        }

        private void Button_Options_Custom_Clear_Click(object sender, RoutedEventArgs e)
        {
            pact.ClearCustoms();
            TriggerListUpdate();
        }

        private void Button_Options_AutoMode_Toggle_Click(object sender, RoutedEventArgs e)
        {
            // TODO: Make auto-mode happen.
        }

        private void Button_Options_About_Click(object sender, RoutedEventArgs e)
        {
            OpenURL("https://github.com/sas41/ProcessAffinityControlTool#readme");
        }

        private void Button_Options_ResetConfig_Click(object sender, RoutedEventArgs e)
        {
            pact.ResetConfig();
            TriggerListUpdate();
            // I don't understand why (yet), but for some reason
            // the performance bars do not update if Reset
            // or Import config buttons are pressed.
            // This harmless hack ensures they update properly. 
            pact.ToggleProcessOverwatch();
            pact.ToggleProcessOverwatch();
        }

        private void OpenURL(string url)
        {
            try
            {
                Process.Start(url);
            }
            catch
            {
                // hack because of this: https://github.com/dotnet/corefx/issues/10361
                if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                {
                    url = url.Replace("&", "^&");
                    Process.Start(new ProcessStartInfo("cmd", $"/c start {url}") { CreateNoWindow = true });
                }
                else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
                {
                    Process.Start("xdg-open", url);
                }
                else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
                {
                    Process.Start("open", url);
                }
                else
                {
                    throw;
                }
            }
        }
    }
}
