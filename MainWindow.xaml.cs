namespace kinectScan
{
    using System;
    using System.ComponentModel;
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
    using System.Windows.Threading;

    using HelixToolkit.Wpf;

    using Microsoft.Kinect;
    using Microsoft.Kinect.Toolkit;

    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        
        /// <summary>
        /// Timestamp of last depth frame in milliseconds
        /// </summary>
        private long lastFrameTimestamp = 0;

        /// <summary>
        /// Timer to count FPS
        /// </summary>
        private DispatcherTimer fpsTimer;

        /// <summary>
        /// Timer stamp of last computation of FPS
        /// </summary>
        private DateTime lastFPSTimestamp;

        /// <summary>
        /// Event interval for FPS timer
        /// </summary>
        private const int FpsInterval = 5;

        /// <summary>
        /// The counter for frames that have been processed
        /// </summary>
        private int processedFrameCount = 0;

        /// <summary>
        /// Active Kinect sensor
        /// </summary>
        private KinectSensor sensor;

        /// <summary>
        /// Kinect sensor chooser object
        /// </summary>
        private KinectSensorChooser sensorChooser;
      
        /// <summary>
        /// Format of depth image to use
        /// </summary>
        private const DepthImageFormat dFormat = DepthImageFormat.Resolution320x240Fps30;

        /// <summary>
        /// Format of color image to use
        /// </summary>
        private const ColorImageFormat cFormat = ColorImageFormat.InfraredResolution640x480Fps30;
        
        //GeometryModel3D[] points = new GeometryModel3D[2*(320 * 240)];
        public int[] Depth = new int[320 * 240];
        Model3DGroup modelGroup = new Model3DGroup();
        public GeometryModel3D msheet = new GeometryModel3D();
        public Point3DCollection corners = new Point3DCollection();
        public Int32Collection Triangles = new Int32Collection();
        public MeshGeometry3D tmesh = new MeshGeometry3D();
        public Vector3DCollection Normals = new Vector3DCollection();
        public PointCollection myTextureCoordinatesCollection = new PointCollection();

        public int s = 4;

        public byte[] colorPixels;
        public WriteableBitmap colorBitmap;

        public MainWindow()
        {
            InitializeComponent();
        }

        private void WindowLoaded(object sender, RoutedEventArgs e)
        {
            // Start Kinect sensor chooser
            this.sensorChooser = new KinectSensorChooser();
            this.sensorChooserUI.KinectSensorChooser = this.sensorChooser;
            this.sensorChooser.KinectChanged += this.OnKinectSensorChanged;
            this.sensorChooser.Start();

            // Start fps timer
            this.fpsTimer = new DispatcherTimer(DispatcherPriority.Send);
            this.fpsTimer.Interval = new TimeSpan(0, 0, FpsInterval);
            this.fpsTimer.Tick += this.FpsTimerTick;
            this.fpsTimer.Start();

            // Set last fps timestamp as now
            this.lastFPSTimestamp = DateTime.Now;
        }
        /// <summary>
        /// Execute shutdown tasks
        /// </summary>
        /// <param name="sender">object sending the event</param>
        /// <param name="e">event arguments</param>
        private void WindowClosing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            // Stop timer
            if (null != this.fpsTimer)
            {
                this.fpsTimer.Stop();
                this.fpsTimer.Tick -= this.FpsTimerTick;
            }

            // Unregister Kinect sensor chooser event
            if (null != this.sensorChooser)
            {
                this.sensorChooser.KinectChanged -= this.OnKinectSensorChanged;
            }

            // Stop sensor
            if (null != this.sensor)
            {
                this.sensor.Stop();
                this.sensor.DepthFrameReady -= this.SensorDepthFrameReady;
                this.sensor.ColorFrameReady -= this.SensorColorFrameReady;
            }
        }

        private void OnKinectSensorChanged(object sender, KinectChangedEventArgs e)
        {
            // Check new sensor's status
            if (this.sensor != e.NewSensor)
            {
                // Stop old sensor
                if (null != this.sensor)
                {
                    this.sensor.Stop();
                    this.sensor.DepthFrameReady -= this.SensorDepthFrameReady;
                    this.sensor.ColorFrameReady -= this.SensorColorFrameReady;
                }

                this.sensor = null;

                if (null != e.NewSensor && KinectStatus.Connected == e.NewSensor.Status)
                {
                    // Start new sensor
                    this.sensor = e.NewSensor;
                    this.StartCameraStream(dFormat, cFormat);
                }

                if (null == this.sensor)
                {
                    this.statusBarText.Content = Properties.Resources.NoKinectReady;
                    this.IR_Title.Content = "";
                    this.Model_Title.Content = "";
                    this.RGB_Title.Content = "";
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
           // if (!this.savingMesh)
            //{
                if (null == this.sensor)
                {
                    // Show "No ready Kinect found!" on status bar
                    this.statusBarTextBlock.Text = Properties.Resources.NoReadyKinect;
                }
                else
                {
                    // Calculate time span from last calculation of FPS
                    double intervalSeconds = (DateTime.Now - this.lastFPSTimestamp).TotalSeconds;

                    // Calculate and show fps on status bar
                    this.statusBarTextBlock.Text = string.Format(
                        System.Globalization.CultureInfo.InvariantCulture,
                        Properties.Resources.Fps,
                        (double)this.processedFrameCount / intervalSeconds);
                }
            //}

            // Reset frame counter
            this.processedFrameCount = 0;
            this.lastFPSTimestamp = DateTime.Now;
        }

        /// <summary>
        /// Reset FPS timer and counter
        /// </summary>
        private void ResetFps()
        {
            // Restart fps timer
            if (null != this.fpsTimer)
            {
                this.fpsTimer.Stop();
                this.fpsTimer.Start();
            }

            // Reset frame counter
            this.processedFrameCount = 0;
            this.lastFPSTimestamp = DateTime.Now;
        }
        /// <summary>
        /// Start depth stream at specific resolution
        /// </summary>
        /// <param name="format">The resolution of image in depth stream</param>
        private void StartCameraStream(DepthImageFormat dFormat, ColorImageFormat cFormat)
        {
            try
            {
                // Enable streams, register event handler and start
                this.sensor.DepthStream.Enable(dFormat);
                this.sensor.DepthFrameReady += this.SensorDepthFrameReady;
                this.sensor.ColorStream.Enable(cFormat);
                this.sensor.ColorFrameReady += this.SensorColorFrameReady;
                
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

            
            
            // Allocate space to put the pixels we'll receive
            this.colorPixels = new byte[this.sensor.ColorStream.FramePixelDataLength];

            //// This is the bitmap we'll display on-screen
            this.colorBitmap = new WriteableBitmap(this.sensor.ColorStream.FrameWidth, this.sensor.ColorStream.FrameHeight, 96.0, 96.0, PixelFormats.Gray16, null);

           

            DirectionalLight DirLight1 = new DirectionalLight();
            DirLight1.Color = Colors.White;
            DirLight1.Direction = new Vector3D(-0.61, -0.5, -0.61);
            PerspectiveCamera Camera1 = new PerspectiveCamera();

            Camera1.Position = new Point3D(0, 4, 30);
            Camera1.LookDirection = new Vector3D(0, 0, -1);
            Camera1.UpDirection = new Vector3D(0, -1, 0);
            corners.Add(new Point3D(-5, -5, -1));
            corners.Add(new Point3D(5, -5, -1));
            corners.Add(new Point3D(5, 5, -1));
            corners.Add(new Point3D(-5, 5, -1));


            corners.Add(new Point3D(5, -5, -1));
            corners.Add(new Point3D(5, -5, -11));
            //corners.Add(new Point3D(5, 5, -11));
            corners.Add(new Point3D(5, 5, -11));
            corners.Add(new Point3D(5, 5, -11));
            // corners.Add(new Point3D(5, 5, -1));

            corners.Add(new Point3D(5, -5, -11));
            corners.Add(new Point3D(-5, -5, -11));
            // corners.Add(new Point3D(-5, 5, -11));
            corners.Add(new Point3D(-5, 5, -11));
            corners.Add(new Point3D(5, 5, -11));
            // corners.Add(new Point3D(5, -5, -11));

            corners.Add(new Point3D(-5, -5, -11));
            corners.Add(new Point3D(5, -5, -11));
            //corners.Add(new Point3D(5, -5, -1));
            corners.Add(new Point3D(5, -5, -1));
            corners.Add(new Point3D(-5, -5, -1));
            // corners.Add(new Point3D(-5, -5, -11));

            corners.Add(new Point3D(-5, -5, -11));
            corners.Add(new Point3D(-5, -5, -1));
            // corners.Add(new Point3D(-5, 5, -1));
            corners.Add(new Point3D(-5, 5, -1));
            corners.Add(new Point3D(-5, 5, -11));
            //corners.Add(new Point3D(-5, -5, -11));

            corners.Add(new Point3D(5, 5, -11));
            corners.Add(new Point3D(-5, 5, -11));
            // corners.Add(new Point3D(-5, 5, -1));
            corners.Add(new Point3D(-5, 5, -1));
            corners.Add(new Point3D(5, 5, -1));
            // corners.Add(new Point3D(5, 5, -11));
            Triangles.Add(0);
            Triangles.Add(1);
            Triangles.Add(2);
            Triangles.Add(3);
            Triangles.Add(4);
            Triangles.Add(5);
            Triangles.Add(6);
            Triangles.Add(7);
            Triangles.Add(8);
            Triangles.Add(9);
            Triangles.Add(10);
            Triangles.Add(11);
            Triangles.Add(12);
            Triangles.Add(13);
            Triangles.Add(14);
            Triangles.Add(15);
            Triangles.Add(16);
            Triangles.Add(17);
            Triangles.Add(18);
            Triangles.Add(19);
            Triangles.Add(20);
            Triangles.Add(21);
            Triangles.Add(22);
            Triangles.Add(23);
           /* Triangles.Add(24);
            Triangles.Add(25);
            Triangles.Add(26);
            Triangles.Add(27);
            Triangles.Add(28);
            Triangles.Add(29);
            Triangles.Add(30);
            Triangles.Add(31);
            Triangles.Add(32);
            Triangles.Add(33);
            Triangles.Add(34);
            Triangles.Add(35); */
            myTextureCoordinatesCollection.Add(new System.Windows.Point(0, 0));
            myTextureCoordinatesCollection.Add(new System.Windows.Point(1, 0));
            myTextureCoordinatesCollection.Add(new System.Windows.Point(1, 1));
            myTextureCoordinatesCollection.Add(new System.Windows.Point(1, 1));
            myTextureCoordinatesCollection.Add(new System.Windows.Point(0, 1));
            myTextureCoordinatesCollection.Add(new System.Windows.Point(0, 0));
            myTextureCoordinatesCollection.Add(new System.Windows.Point(1, 0));
            myTextureCoordinatesCollection.Add(new System.Windows.Point(0, 0));
            myTextureCoordinatesCollection.Add(new System.Windows.Point(0, 1));
            myTextureCoordinatesCollection.Add(new System.Windows.Point(0, 1));
            myTextureCoordinatesCollection.Add(new System.Windows.Point(1, 1));
            myTextureCoordinatesCollection.Add(new System.Windows.Point(1, 0));
            myTextureCoordinatesCollection.Add(new System.Windows.Point(0, 0));
            myTextureCoordinatesCollection.Add(new System.Windows.Point(1, 0));
            myTextureCoordinatesCollection.Add(new System.Windows.Point(1, 1));
            myTextureCoordinatesCollection.Add(new System.Windows.Point(1, 1));
            myTextureCoordinatesCollection.Add(new System.Windows.Point(0, 1));
            myTextureCoordinatesCollection.Add(new System.Windows.Point(0, 0));
            myTextureCoordinatesCollection.Add(new System.Windows.Point(1, 0));
            myTextureCoordinatesCollection.Add(new System.Windows.Point(0, 0));
            myTextureCoordinatesCollection.Add(new System.Windows.Point(0, 1));
            myTextureCoordinatesCollection.Add(new System.Windows.Point(0, 1));
            myTextureCoordinatesCollection.Add(new System.Windows.Point(1, 1));
            myTextureCoordinatesCollection.Add(new System.Windows.Point(1, 0));
            /*myTextureCoordinatesCollection.Add(new System.Windows.Point(0, 0));
            myTextureCoordinatesCollection.Add(new System.Windows.Point(1, 0));
            myTextureCoordinatesCollection.Add(new System.Windows.Point(1, 1));
            myTextureCoordinatesCollection.Add(new System.Windows.Point(1, 1));
            myTextureCoordinatesCollection.Add(new System.Windows.Point(0, 1));
            myTextureCoordinatesCollection.Add(new System.Windows.Point(0, 0));
            myTextureCoordinatesCollection.Add(new System.Windows.Point(1, 0));
            myTextureCoordinatesCollection.Add(new System.Windows.Point(0, 0));
            myTextureCoordinatesCollection.Add(new System.Windows.Point(0, 1));
            myTextureCoordinatesCollection.Add(new System.Windows.Point(0, 1));
            myTextureCoordinatesCollection.Add(new System.Windows.Point(1, 1));
            myTextureCoordinatesCollection.Add(new System.Windows.Point(1, 0)); */
            Normals.Add(new Vector3D(0, 0, 1));
            Normals.Add(new Vector3D(0, 0, 1));
            Normals.Add(new Vector3D(0, 0, 1));
            Normals.Add(new Vector3D(0, 0, 1));
            Normals.Add(new Vector3D(0, 0, 1));
            Normals.Add(new Vector3D(0, 0, 1));
            Normals.Add(new Vector3D(0, 0, 1));
            Normals.Add(new Vector3D(0, 0, 1));
            Normals.Add(new Vector3D(0, 0, 1));
            Normals.Add(new Vector3D(0, 0, 1));
            Normals.Add(new Vector3D(0, 0, 1));
            Normals.Add(new Vector3D(0, 0, 1));
            Normals.Add(new Vector3D(0, 0, 1));
            Normals.Add(new Vector3D(0, 0, 1));
            Normals.Add(new Vector3D(0, 0, 1));
            Normals.Add(new Vector3D(0, 0, 1));
            Normals.Add(new Vector3D(0, 0, 1));
            Normals.Add(new Vector3D(0, 0, 1));
            Normals.Add(new Vector3D(0, 0, 1));
            Normals.Add(new Vector3D(0, 0, 1));
            Normals.Add(new Vector3D(0, 0, 1));
            Normals.Add(new Vector3D(0, 0, 1));
            Normals.Add(new Vector3D(0, 0, 1));
            Normals.Add(new Vector3D(0, 0, 1));
           /* Normals.Add(new Vector3D(0, 0, 1));
            Normals.Add(new Vector3D(0, 0, 1));
            Normals.Add(new Vector3D(0, 0, 1));
            Normals.Add(new Vector3D(0, 0, 1));
            Normals.Add(new Vector3D(0, 0, 1));
            Normals.Add(new Vector3D(0, 0, 1));
            Normals.Add(new Vector3D(0, 0, 1));
            Normals.Add(new Vector3D(0, 0, 1));
            Normals.Add(new Vector3D(0, 0, 1));
            Normals.Add(new Vector3D(0, 0, 1));
            Normals.Add(new Vector3D(0, 0, 1));
            Normals.Add(new Vector3D(0, 0, 1)); */
            tmesh.Positions = corners;
            tmesh.TriangleIndices = Triangles;
            tmesh.Normals = Normals;
            tmesh.TextureCoordinates = myTextureCoordinatesCollection;
            msheet.Geometry = tmesh;
            msheet.Transform = new TranslateTransform3D(0, 0, 0);
            //msheet.Material = new DiffuseMaterial((SolidColorBrush)(new BrushConverter().ConvertFrom("#52318F")));
            LinearGradientBrush myHorizontalGradient = new LinearGradientBrush();
            myHorizontalGradient.StartPoint = new System.Windows.Point(0, 0.5);
            myHorizontalGradient.EndPoint = new System.Windows.Point(1, 0.5);
            myHorizontalGradient.GradientStops.Add(new GradientStop(Colors.Yellow, 0.0));
            myHorizontalGradient.GradientStops.Add(new GradientStop(Colors.Red, 0.25));
            myHorizontalGradient.GradientStops.Add(new GradientStop(Colors.Blue, 0.75));
            myHorizontalGradient.GradientStops.Add(new GradientStop(Colors.LimeGreen, 1.0));
            DiffuseMaterial myMaterial = new DiffuseMaterial(myHorizontalGradient);
            msheet.Material = myMaterial;
            RotateTransform3D myRotateTransform3D = new RotateTransform3D();
            AxisAngleRotation3D myAxisAngleRotation3d = new AxisAngleRotation3D();
            myAxisAngleRotation3d.Axis = new Vector3D(3, 3, 0);

            //myAxisAngleRotation3d.Angle = 40;
            //myRotateTransform3D.Rotation = myAxisAngleRotation3d;
            //msheet.Transform = myRotateTransform3D;
            this.modelGroup.Children.Add(msheet);
            this.modelGroup.Children.Add(DirLight1);
            ModelVisual3D modelsVisual = new ModelVisual3D();
            modelsVisual.Content = this.modelGroup;
            Viewport3D myViewport = new Viewport3D();
            myViewport.IsHitTestVisible = false;
            myViewport.Camera = Camera1;
            myViewport.Children.Add(modelsVisual);
            KinectNormalView.Children.Add(myViewport);
            myViewport.Height = KinectNormalView.Height;
            myViewport.Width = KinectNormalView.Width;
            Canvas.SetTop(myViewport, 0);
            Canvas.SetLeft(myViewport, 0);

        }

        /// <summary>
        /// Event handler for Kinect sensor's ColorFrameReady event
        /// </summary>
        /// <param name="sender">object sending the event</param>
        /// <param name="e">event arguments</param>
        void SensorColorFrameReady(object sender, ColorImageFrameReadyEventArgs e)
        {
            using (ColorImageFrame colorFrame = e.OpenColorImageFrame())
            {
                if (colorFrame != null)
                {
                    // Copy the pixel data from the image to a temporary array
                    colorFrame.CopyPixelDataTo(this.colorPixels);

                    // Write the pixel data into our bitmap
                    this.colorBitmap.WritePixels(
                        new Int32Rect(0, 0, this.colorBitmap.PixelWidth, this.colorBitmap.PixelHeight),
                        this.colorPixels,
                        this.colorBitmap.PixelWidth * colorFrame.BytesPerPixel,
                        0);
                }

                this.KinectRGBView.Source = this.colorBitmap;

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


                int i = 0;

                for (int y = s; y < (240-s); y = y + s)
                {
                    for (int x = s; x < (320-s); x = x + s)
                    {
                        this.Depth[x + y * 320] = ((ushort)pixelData[x + y * 320]) >> 3;
                        //filter depth
                        this.Depth[x + y * 320] = (this.Depth[x + y * 320] >= minDepth && this.Depth[x + y * 320] <= maxDepth ? this.Depth[x + y * 320] : (ushort)maxDepth);
                        //((TranslateTransform3D)points[i].Transform).OffsetZ = ((this.Depth[x + ((y+s) * 320)]) + (this.Depth[(x+s) + ((y+s) * 320)])+(this.Depth[(x+s) + (y * 320)]))/3;
                       // ((TranslateTransform3D)points[i + 1].Transform).OffsetZ = ((this.Depth[(x+s) + (y * 320)]) + (this.Depth[x  + (y  * 320)]) + (this.Depth[x  + ((y+s) * 320)]))/3;
                        i=i+2;

                    }
                }

                this.KinectDepthView.Source = DepthToBitmapSource(imageFrame);
            }
        }
       
        //////private GeometryModel3D TopTriangle(int x, int y, int s)
        //////{
        //////    int i = 0;
        //////    Point3DCollection corners = new Point3DCollection();
        //////    corners.Add(new Point3D(x, y + s, this.Depth[x + ((y + s) * 320)]));
        //////    corners.Add(new Point3D(x + s, y + s, this.Depth[(x + s) + ((y + s) * 320)]));
        //////    corners.Add(new Point3D(x + s, y, this.Depth[(x + s) + (y * 320)]));
            
        //////    Int32Collection Triangles = new Int32Collection();
        //////    Triangles.Add(i);
        //////    Triangles.Add(i + 1);
        //////    Triangles.Add(i + 2);
        //////    i = i + 3;

        //////    MeshGeometry3D tmesh = new MeshGeometry3D();
        //////    tmesh.Positions = corners;
        //////    tmesh.TriangleIndices = Triangles;
        //////    tmesh.Normals.Add(new Vector3D((this.Depth[x + (y * 320)]) - (this.Depth[x + ((y + s) * 320)]), (this.Depth[(x + s) + ((y + s) * 320)]) - (this.Depth[x + ((y + s) * 320)]), 1));
        //////    tmesh.Normals.Add(new Vector3D((this.Depth[(x + s) + (y * 320)]) - (this.Depth[(x + s) + ((y + s) * 320)]), (this.Depth[x + ((y + s) * 320)]) - (this.Depth[(x + s) + ((y + s) * 320)]), 1));
        //////    tmesh.Normals.Add(new Vector3D((this.Depth[x + (y * 320)]) - (this.Depth[(x + s) + (y * 320)]), (this.Depth[(x + s) + ((y + s) * 320)]) - (this.Depth[(x + s) + (y * 320)]), 1));

        //////    GeometryModel3D msheet = new GeometryModel3D();
        //////    msheet.Geometry = tmesh;
        //////    msheet.Material = new DiffuseMaterial((SolidColorBrush)(new BrushConverter().ConvertFrom("#52318F")));
        //////    return msheet;
        //////}

        //////private GeometryModel3D BottomTriangle(int x, int y, int s)
        //////{
        //////    int i = 0;
        //////    Point3DCollection corners = new Point3DCollection();
        //////    corners.Add(new Point3D(x + s, y, this.Depth[(x + s) + (y * 320)]));
        //////    corners.Add(new Point3D(x, y, this.Depth[x + (y * 320)]));
        //////    corners.Add(new Point3D(x, y + s, this.Depth[x + ((y + s) * 320)]));

        //////    Int32Collection Triangles = new Int32Collection();
        //////    Triangles.Add(i);
        //////    Triangles.Add(i + 1);
        //////    Triangles.Add(i + 2);
        //////    i = i + 3;

        //////    MeshGeometry3D tmesh = new MeshGeometry3D();
        //////    tmesh.Positions = corners;
        //////    tmesh.TriangleIndices = Triangles;
        //////    tmesh.Normals.Add(new Vector3D((this.Depth[x + (y * 320)]) - (this.Depth[(x + s) + (y * 320)]), (this.Depth[(x + s) + ((y + s) * 320)]) - (this.Depth[(x + s) + (y * 320)]), 1));
        //////    tmesh.Normals.Add(new Vector3D((this.Depth[x + ((y + s) * 320)]) - (this.Depth[x + (y * 320)]), (this.Depth[(x + s) + (y * 320)]) - (this.Depth[x + (y * 320)]), 1));
        //////    tmesh.Normals.Add(new Vector3D((this.Depth[x + (y * 320)]) - (this.Depth[x + ((y + s) * 320)]), (this.Depth[(x + s) + ((y + s) * 320)]) - (this.Depth[x + ((y + s) * 320)]), 1));

        //////    GeometryModel3D msheet = new GeometryModel3D();
        //////    msheet.Geometry = tmesh;
        //////    msheet.Material = new DiffuseMaterial((SolidColorBrush)(new BrushConverter().ConvertFrom("#52318F")));
        //////    return msheet;
        //////}


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
            return;
        }
        private void test_Click(object sender, RoutedEventArgs e)
        {
            return;
        }

        private void Export_Model_Click(object sender, RoutedEventArgs e)
        {            
            string fileName = "model.obj";
            
            using (var exporter = new ObjExporter(fileName))
            {
                exporter.Export(this.modelGroup);
            }

            Process.Start("explorer.exe", "/select,\"" + fileName + "\"");

        }

        /// <summary>
        /// Show exception info on status bar
        /// </summary>
        /// <param name="message">Message to show on status bar</param>
        private void ShowStatusMessage(string message)
        {
            this.Dispatcher.BeginInvoke((Action)(() =>
            {
                //this.ResetFps();
                this.statusBarTextBlock.Text = message;
            }));
        }
    }
}