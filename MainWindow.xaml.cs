
namespace kinectScan
{
    using System;
    using System.Globalization;
    using System.IO;
    using System.Threading.Tasks;
    using System.Drawing;
    using System.Diagnostics;
    using System.Windows;
    using System.Windows.Controls;
    using System.Windows.Media;
    using System.Windows.Media.Imaging;
    using System.Windows.Media.Media3D;

    using Microsoft.Kinect;
    using Microsoft.Kinect.Toolkit;
    using Microsoft.Kinect.Toolkit.Fusion;


    /// <summary>
    /// A struct containing depth image pixels and frame timestamp
    /// </summary>
    internal struct DepthData
    {
        public DepthImagePixel[] DepthImagePixels;
        public long FrameTimestamp;
    }

    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        #region Variables
        /// <summary>
        /// Track whether Dispose has been called
        /// </summary>
        private bool disposed;

        /// <summary>
        /// Image width of depth frame
        /// </summary>
        private int width = 0;

        /// <summary>
        /// Image height of depth frame
        /// </summary>
        private int height = 0;

        /// <summary>
        /// Format of depth image to use
        /// </summary>
        private const DepthImageFormat ImageFormat = DepthImageFormat.Resolution320x240Fps30;

        /// <summary>
        /// Active Kinect sensor
        /// </summary>
        private KinectSensor sensor;

        /// <summary>
        /// Kinect sensor chooser object
        /// </summary>
        private KinectSensorChooser sensorChooser;

        /// <summary>
        /// Storage for 3D model
        /// </summary>
        GeometryModel3D[] points = new GeometryModel3D[320 * 240];

        Model3DGroup modelGroup = new Model3DGroup();


        int s = 2;
        #endregion

        public MainWindow()
        {
            InitializeComponent();
        }

        #region Garbage
        /// <summary>
        /// Finalizes an instance of the MainWindow class.
        /// This destructor will run only if the Dispose method does not get called.
        /// </summary>
        ~MainWindow()
        {
            this.Dispose(false);
        }

        /// <summary>
        /// Dispose resources
        /// </summary>
        public void Dispose()
        {
            this.Dispose(true);

            // This object will be cleaned up by the Dispose method.
            GC.SuppressFinalize(this);
        }

        /// <summary>
        /// Frees all memory associated with the FusionImageFrame.
        /// </summary>
        /// <param name="disposing">Whether the function was called from Dispose.</param>
        protected virtual void Dispose(bool disposing)
        {
            /* if (!this.disposed)
             {
                 if (disposing)
                 {
                     if (null != this.depthFloatFrame)
                     {
                         this.depthFloatFrame.Dispose();
                     }

                     if (null != this.deltaFromReferenceFrame)
                     {
                         this.deltaFromReferenceFrame.Dispose();
                     }

                     if (null != this.shadedSurfaceFrame)
                     {
                         this.shadedSurfaceFrame.Dispose();
                     }

                     if (null != this.shadedSurfaceNormalsFrame)
                     {
                         this.shadedSurfaceNormalsFrame.Dispose();
                     }

                     if (null != this.pointCloudFrame)
                     {
                         this.pointCloudFrame.Dispose();
                     }

                     if (null != this.volume)
                     {
                         this.volume.Dispose();
                     }
                 }
             } */

            this.disposed = true;
        }
        #endregion

        /// <summary>
        /// Execute startup tasks
        /// </summary>
        /// <param name="sender">object sending the event</param>
        /// <param name="e">event arguments</param>
        private void WindowLoaded(object sender, RoutedEventArgs e)
        {
            
            //this.sensorChooser = new KinectSensorChooser();
            //this.sensorChooserUI.KinectSensorChooser = this.sensorChooser;
            //this.sensorChooser.KinectChanged += this.OnKinectSensorChanged;
            //this.sensorChooser.Start();

            // Look through all sensors and start the first connected one.
            // This requires that a Kinect is connected at the time of app startup.
            // To make your app robust against plug/unplug, 
            // it is recommended to use KinectSensorChooser provided in Microsoft.Kinect.Toolkit
            foreach (var potentialSensor in KinectSensor.KinectSensors)
            {
                if (potentialSensor.Status == KinectStatus.Connected)
                {
                    this.sensor = potentialSensor;
                    break;
                }
            }


            if (null != this.sensor)
            {
                //Start the depth stream
               this.sensor.DepthStream.Enable(DepthImageFormat.Resolution320x240Fps30);

                DirectionalLight DirLight1 = new DirectionalLight();
                DirLight1.Color = Colors.White;
                DirLight1.Direction = new Vector3D(1, 1, 1);

                PerspectiveCamera Camera1 = new PerspectiveCamera();
                Camera1.FarPlaneDistance = 8000;
                Camera1.NearPlaneDistance = 100;
                Camera1.FieldOfView = 10;
                Camera1.Position = new Point3D(160, 120, -1000);
                Camera1.LookDirection = new Vector3D(0, 0, 1);
                Camera1.UpDirection = new Vector3D(0, -1, 0);

                //Model3DGroup modelGroup = new Model3DGroup();

                int i = 0;
                for (int y = 0; y < 240; y += s)
                {
                    for (int x = 0; x < 320; x += s)
                    {
                        points[i] = Triangle(x, y, s);
                        points[i].Transform = new TranslateTransform3D(0, 0, 0);
                        this.modelGroup.Children.Add(points[i]);
                        i++;
                    }
                }

                this.modelGroup.Children.Add(DirLight1);
                ModelVisual3D modelsVisual = new ModelVisual3D();
                modelsVisual.Content = this.modelGroup;
                Viewport3D myViewport = new Viewport3D();
                myViewport.IsHitTestVisible = false;
                myViewport.Camera = Camera1;
                myViewport.Children.Add(modelsVisual);
                KinectNormalView.Children.Add(myViewport);

                //this.test_text.Text = this.modelGroup.ToString();
                myViewport.Height = KinectNormalView.Height;
                myViewport.Width = KinectNormalView.Width;
                Canvas.SetTop(myViewport, 0);
                Canvas.SetLeft(myViewport, 0);

                // Add an event handler to be called whenever there is new depth frame data available
                this.sensor.DepthFrameReady += this.SensorDepthFrameReady;
            
             // Start the sensor!
                try
                {
                    this.sensor.Start();
                }
                catch (IOException)
                {
                    this.sensor = null;
                }
            }

            if (null == this.sensor)
            {
                this.statusBarText.Content = Properties.Resources.NoKinectReady;
                this.IR_Title.Content = "";
                this.Model_Title.Content = "";
                this.Bilateral_Title.Content = "";
            }
        }

        /// <summary>
        /// Execute shutdown tasks
        /// </summary>
        /// <param name="sender">object sending the event</param>
        /// <param name="e">event arguments</param>
        private void WindowClosing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            // Stop timer
            /* if (null != this.fpsTimer)
             {
                 this.fpsTimer.Stop();
                 this.fpsTimer.Tick -= this.FpsTimerTick;
             }*/

            // Unregister Kinect sensor chooser event
            if (null != this.sensorChooser)
            {
                this.sensorChooser.KinectChanged -= this.OnKinectSensorChanged;
            }

            // Stop sensor
            if (null != this.sensor)
            {
                this.sensor.Stop();
                this.sensor.DepthFrameReady -= this.OnDepthFrameReady;
            }
        }

        /// <summary>
        /// Handler function for Kinect changed event
        /// </summary>
        /// <param name="sender">Event generator</param>
        /// <param name="e">Event parameter</param>
        private void OnKinectSensorChanged(object sender, KinectChangedEventArgs e)
        {
            // Check new sensor's status
            if (this.sensor != e.NewSensor)
            {
                // Stop old sensor
                if (null != this.sensor)
                {
                    this.sensor.Stop();
                    this.sensor.DepthFrameReady -= this.OnDepthFrameReady;
                }

                this.sensor = null;

                if (null != e.NewSensor && KinectStatus.Connected == e.NewSensor.Status)
                {
                    // Start new sensor
                    this.sensor = e.NewSensor;
                    this.StartDepthStream(ImageFormat);
                }
            }
        }

        /// <summary>
        /// Handler for FPS timer tick
        /// </summary>
        /// <param name="sender">Object sending the event</param>
        /// <param name="e">Event arguments</param>
        private void FpsTimerTick(object sender, EventArgs e)
        {
          /*  if (!this.savingMesh)
            {
                if (null == this.sensor)
                {
                    // Show "No ready Kinect found!" on status bar
                    this.statusBarText.Text = Properties.Resources.NoReadyKinect;
                }
                else
                {
                    // Calculate time span from last calculation of FPS
                    double intervalSeconds = (DateTime.Now - this.lastFPSTimestamp).TotalSeconds;

                    // Calculate and show fps on status bar
                    this.statusBarText.Text = string.Format(
                        System.Globalization.CultureInfo.InvariantCulture,
                        Properties.Resources.Fps,
                        (double)this.processedFrameCount / intervalSeconds);
                }
            }

            // Reset frame counter
            this.processedFrameCount = 0;
            this.lastFPSTimestamp = DateTime.Now; */
        }

        /// <summary>
        /// Reset FPS timer and counter
        /// </summary>
        private void ResetFps()
        {
           /* // Restart fps timer
            if (null != this.fpsTimer)
            {
                this.fpsTimer.Stop();
                this.fpsTimer.Start();
            }

            // Reset frame counter
            this.processedFrameCount = 0;
            this.lastFPSTimestamp = DateTime.Now; */
        }

        /// <summary>
        /// Start depth stream at specific resolution
        /// </summary>
        /// <param name="format">The resolution of image in depth stream</param>
        private void StartDepthStream(DepthImageFormat format)
        {
            try
            {
                // Enable depth stream, register event handler and start
                this.sensor.DepthStream.Enable(format);
                this.sensor.DepthFrameReady += this.OnDepthFrameReady;
                this.sensor.Start();
            }
            catch (IOException ex)
            {
                // Device is in use
                this.sensor = null;
                this.ShowStatusMessage(ex.Message);

                return;
            }
            catch (InvalidOperationException ex)
            {
                // Device is not valid, not supported or hardware feature unavailable
                this.sensor = null;
                this.ShowStatusMessage(ex.Message);

                return;
            }
            /*
            // Create volume
            if (this.RecreateReconstruction())
            {
                // Show introductory message
                this.ShowStatusMessage(Properties.Resources.IntroductoryMessage);
            } */
        }

        /// <summary>
        /// Event handler for Kinect sensor's DepthFrameReady event
        /// </summary>
        /// <param name="sender">object sending the event</param>
        /// <param name="e">event arguments</param>
        private void OnDepthFrameReady(object sender, DepthImageFrameReadyEventArgs e)
        {
            // Open depth frame
            using (DepthImageFrame depthFrame = e.OpenDepthImageFrame())
            {
                if (null != depthFrame) // && !this.processing)
                {
                    DepthData depthData = new DepthData();

                    // Save frame timestamp
                    depthData.FrameTimestamp = depthFrame.Timestamp;

                    // Create local depth pixels buffer
                    depthData.DepthImagePixels = new DepthImagePixel[depthFrame.PixelDataLength];

                    // Copy depth pixels to local buffer
                    depthFrame.CopyDepthImagePixelDataTo(depthData.DepthImagePixels);

                    this.width = depthFrame.Width;
                    this.height = depthFrame.Height;

                    // Use dispatcher object to invoke ProcessDepthData function to process
                    /*this.Dispatcher.BeginInvoke(
                                        DispatcherPriority.Background,
                                        (Action<DepthData>)((d) => { this.ProcessDepthData(d); }),
                                        depthData);

                    // Mark one frame will be processed
                    this.processing = true;*/
                }
            }
        }

        /// <summary>
        /// Event handler for Kinect sensor's DepthFrameReady event
        /// Take in depth data
        /// </summary>
        /// <param name="sender">object sending the event</param>
        /// <param name="e">event arguments</param>
        void SensorDepthFrameReady(object sender, DepthImageFrameReadyEventArgs e)
        {

            DepthImageFrame imageFrame = e.OpenDepthImageFrame();
            if (imageFrame != null)
            {
                short[] pixelData = new short[imageFrame.PixelDataLength];
                imageFrame.CopyPixelDataTo(pixelData);

                // Get the min and max reliable depth for the current frame
                double minDepth = Near_Filter_Slider.Value;
                double maxDepth = Far_Filter_Slider.Value;

                int temp = 0;
                int i = 0;

                for (int y = 0; y < 240; y += s)
                {
                    for (int x = 0; x < 320; x += s)
                    {
                        temp = ((ushort)pixelData[x + y * 320]) >> 3;
                        //filter depth
                        temp = (temp >= minDepth && temp <= maxDepth ? temp : -1001);
                        ((TranslateTransform3D)points[i].Transform).OffsetZ = temp;
                        i++;

                    }
                }

                this.KinectDepthView.Source = DepthToBitmapSource(imageFrame);
            }
        }

        private GeometryModel3D Triangle(double x, double y, double s)
        {
            Point3DCollection corners = new Point3DCollection();
            corners.Add(new Point3D(x, y, 0));
            corners.Add(new Point3D(x, y + s, 0));
            corners.Add(new Point3D(x + s, y + s, 0));
            corners.Add(new Point3D(x + s, y, 0));

            Int32Collection Triangles = new Int32Collection();
            Triangles.Add(0);
            Triangles.Add(1);
            Triangles.Add(2);
            Triangles.Add(3);

            MeshGeometry3D tmesh = new MeshGeometry3D();
            tmesh.Positions = corners;
            tmesh.TriangleIndices = Triangles;
            tmesh.Normals.Add(new Vector3D(x, y, -1));

            GeometryModel3D msheet = new GeometryModel3D();
            msheet.Geometry = tmesh;
            msheet.Material = new DiffuseMaterial((SolidColorBrush)(new BrushConverter().ConvertFrom("#52318F")));
            return msheet;
        }

        BitmapSource DepthToBitmapSource(DepthImageFrame imageFrame)
        {
            short[] pixelData = new short[imageFrame.PixelDataLength];
            imageFrame.CopyPixelDataTo(pixelData);

            BitmapSource bmap = BitmapSource.Create(
             imageFrame.Width,
             imageFrame.Height,
             96, 96,
             PixelFormats.Gray16,
             null,
             pixelData,
             imageFrame.Width * imageFrame.BytesPerPixel);
            return bmap;
        }

        private void Begin_Scan_Click(object sender, RoutedEventArgs e)
        {

        }

        /// <summary>
        /// Show exception info on status bar
        /// </summary>
        /// <param name="message">Message to show on status bar</param>
        private void ShowStatusMessage(string message)
        {
            this.Dispatcher.BeginInvoke((Action)(() =>
            {
                this.ResetFps();
                this.statusBarText.Content = message;
            }));
        }

        private void Export_Model_Click(object sender, RoutedEventArgs e)
        {
            return;
        }

        private void test_Click(object sender, RoutedEventArgs e)
        {
            return;
        }
    }
}

