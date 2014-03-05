using RobotKit;
using RobotKit.Internal;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Threading.Tasks;
using Windows.ApplicationModel;
using Windows.Devices.Sensors;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.UI;
using Windows.UI.Core;
using Windows.UI.Popups;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Data;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Navigation;

namespace SpheroRace
{    
    public sealed partial class MainPage : Page
    {
        private Sphero m_robot = null;
        private long   m_lastTimeMs;
        private double m_currentX = 0;
        private double m_currentY = 0;

        private bool m_isStarted = false;

        private const string c_noSpheroConnected = "No Sphero Connected";
        private const string c_connectingToSphero = "Connecting to {0}";
        private const string c_spheroConnected = "Connected to {0}";

        public MainPage()
        {
            this.InitializeComponent();
        }
         
        //Начало работы. Переход на основной экран
        protected override void OnNavigatedTo(NavigationEventArgs e)
        {
            base.OnNavigatedTo(e);

            SetupRobotConnection();
            Application app = Application.Current;
            app.Suspending += OnSuspending;
            
            Accelerometer _accelerometer = Accelerometer.GetDefault();    
            _accelerometer.ReadingChanged += new TypedEventHandler<Accelerometer, AccelerometerReadingChangedEventArgs>(ReadingChanged);

        }

        //Завершение работы. Переход с основного экрана
        protected override void OnNavigatedFrom(NavigationEventArgs e)
        {
            base.OnNavigatedFrom(e);

            ShutdownRobotConnection();

            Application app = Application.Current;
            app.Suspending -= OnSuspending;

            Accelerometer _accelerometer = Accelerometer.GetDefault();
            _accelerometer.ReadingChanged -= new TypedEventHandler<Accelerometer, AccelerometerReadingChangedEventArgs>(ReadingChanged);
        }

        //handle the application entering the background
        private void OnSuspending(object sender, SuspendingEventArgs args)
        {
            ShutdownRobotConnection();
        }

        //Ищем робота и подключаемся к нему
        private void SetupRobotConnection()
        {
            SpheroName.Text = c_noSpheroConnected;

            RobotProvider provider = RobotProvider.GetSharedProvider();
            provider.DiscoveredRobotEvent += OnRobotDiscovered;
            provider.NoRobotsEvent += OnNoRobotsEvent;
            provider.ConnectedRobotEvent += OnRobotConnected;
            provider.FindRobots();
        }            

        //Завершаем работу с роботом
        private void ShutdownRobotConnection()
        {
            if (m_robot != null && m_robot.ConnectionState == ConnectionState.Connected)
            {
                m_robot.SensorControl.StopAll();
                m_robot.CollisionControl.StopDetection();
                m_robot.Sleep();                
                m_robot.Disconnect();

                ConnectionToggle.OffContent = "Disconnected";
                SpheroName.Text = c_noSpheroConnected;
                SetRedColor();
    
                RobotProvider provider = RobotProvider.GetSharedProvider();
                provider.DiscoveredRobotEvent -= OnRobotDiscovered;
                provider.NoRobotsEvent -= OnNoRobotsEvent;
                provider.ConnectedRobotEvent -= OnRobotConnected;
            }
        }
       
        //Робот найден!
        private void OnRobotDiscovered(object sender, Robot robot)
        {          
            if (m_robot == null)
            {                
                RobotProvider provider = RobotProvider.GetSharedProvider();
                provider.ConnectRobot(robot);
                ConnectionToggle.OnContent = "Connecting...";
                m_robot = (Sphero)robot;

                SpheroName.Text = string.Format(c_connectingToSphero, robot.BluetoothName);
            }
        }

        //Робот не найден :(
        private void OnNoRobotsEvent(object sender, EventArgs e)
        {
            MessageDialog dialog = new MessageDialog(c_noSpheroConnected);
            dialog.DefaultCommandIndex = 0;
            dialog.CancelCommandIndex = 1;
            dialog.ShowAsync();
        }
    
        //Робот готов исполнять команды
        private void OnRobotConnected(object sender, Robot robot)
        {
            ConnectionToggle.IsOn = true;
            ConnectionToggle.OnContent = "Connected";

            SpheroName.Text = string.Format(c_spheroConnected, robot.BluetoothName);

            if (m_robot != null && m_robot.ConnectionState == ConnectionState.Connected)
            {
                m_robot.CollisionControl.StartDetectionForWallCollisions();
                m_robot.CollisionControl.CollisionDetectedEvent += OnCollisionDetectedEvent;

                SetRedColor();
            }
        }

        //Событие изменения показаний акселерометра
        async private void ReadingChanged(object sender, AccelerometerReadingChangedEventArgs e)
        {
            if (!m_isStarted)
                return;

            await Dispatcher.RunAsync(CoreDispatcherPriority.Normal, () =>
            {
                Windows.Devices.Sensors.AccelerometerReading reading = e.Reading;
                SendRollCommand(reading.AccelerationX, reading.AccelerationY, reading.AccelerationZ);
            });
        }

        //Событие столкновения с препятствием
        private void OnCollisionDetectedEvent(object sender, RobotKit.CollisionData data)
        {
            SetRedColor();
            m_robot.Roll(0, 0);
        }
        
        //Кнопка для отключения робота
        private void ConnectionToggle_Toggled(object sender, RoutedEventArgs e)
        {
            ConnectionToggle.OnContent = "Connecting...";
            if (ConnectionToggle.IsOn)
            {
                if (m_robot == null || m_robot.ConnectionState != ConnectionState.Connected)
                {
                    SetupRobotConnection();
                }
            }
            else
            {
                ShutdownRobotConnection();
            }
        }

        //Запустить робота
        private void StartBtn_Click(object sender, RoutedEventArgs e)
        {
            m_isStarted = true;
            StartBtn.IsEnabled = false;
            StopBtn.IsEnabled = true; 
            
            SetGreenColor();
            m_robot.Roll(0, 0);           
        }

        //Остановить робота
        private void StopBtn_Click(object sender, RoutedEventArgs e)
        {
            m_isStarted = false;
            StartBtn.IsEnabled = true;
            StopBtn.IsEnabled = false;

            SetRedColor();
            m_robot.Roll(0, 0);
        }

        //Начальное состояние робота. Он зеленый и неподвижно стоит
        private void SetGreenColor()
        {
            m_robot.SetHeading(0);
            m_robot.SetBackLED(1.0f);
            m_robot.SetRGBLED(0, 255, 0);
        }

        //Состояние остановки или столкновения в препятствием
        private void SetRedColor()
        {
            m_robot.SetHeading(0);
            m_robot.SetBackLED(1.0f);
            m_robot.SetRGBLED(255, 0, 0);
        }

        //Логика движения робота при изменении показаний акселерометра
        private async void SendRollCommand(double newX, double newY, double newZ)
        {
            float x = (float)newX;
            float y = (float)newY;
            float z = (float)newZ;

            float speed = Math.Abs(z);
            speed = (speed == 0) ? 0 : (float)Math.Sqrt(speed);
            if (speed > 1f)
                speed = 0.01f;

            int heading = Convert.ToInt32(Math.PI / 2.0 - Math.Atan2((double)y - m_currentY, (double)x - m_currentX));

            long milliseconds = DateTime.Now.Ticks / TimeSpan.TicksPerMillisecond;
            if ((milliseconds - m_lastTimeMs) > 1000)
            {
                SetGreenColor();
                m_robot.Roll(heading, speed);

                m_lastTimeMs = milliseconds;
                m_currentX = x;
                m_currentY = y;
            }
        }
       
    }
}