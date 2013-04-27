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

        // stores furthest depth in the scene
        public ushort greatestDepth = 0;

        // array for all of the depth data
        private int[] Depth = new int[320 * 240];

        // stores all of the 3D trianlges with normals and points
        Model3DGroup modelGroup = new Model3DGroup();

        // material placed over the mesh for viewing
        public GeometryModel3D msheet = new GeometryModel3D();

        // collection of corners for the triangles
        public Point3DCollection corners = new Point3DCollection();

        // collection of all the triangles
        public Int32Collection Triangles = new Int32Collection();

        
        public MeshGeometry3D tmesh = new MeshGeometry3D();

        // collection of all the cross product normals
        public Vector3DCollection Normals = new Vector3DCollection();

        // add texture to the mesh
        public PointCollection myTextureCoordinatesCollection = new PointCollection();

        // storage for camera, scene, etc...
        public ModelVisual3D modelsVisual = new ModelVisual3D();

       
        public Viewport3D myViewport = new Viewport3D();

        // test variable
        public int samplespot;

        // variable for changing the quality 1 is the best 16 contains almost no data
        public int s = 1;

        // depth point collection
        public int[] depths_array = new int[4];

        // collection of points
        Point3D[] points_array = new Point3D[4];

        // collection of vectors
        Vector3D[] vectors_array = new Vector3D[5];

        //used for displaying RGB camera
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
            
            // Empty the canvas
            this.ClearMesh();
        }

        /// <summary>
        /// Handles adding a new kinect
        /// </summary>
        /// <param name="sender">object sending the event</param>
        /// <param name="e">event arguments for the newly connected Kinect</param>
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
            }

            if (null == this.sensor)
            {   
                // if no kinect clear the text on screen
                this.statusBarText.Content = Properties.Resources.NoKinectReady;
                this.IR_Title.Content = "";
                this.Model_Title.Content = "";
                this.RGB_Title.Content = "";
            }
        }

        /// <summary>
        /// Handler for FPS timer tick
        /// </summary>
        /// <param name="sender">Object sending the event</param>
        /// <param name="e">Event arguments</param>
        private void FpsTimerTick(object sender, EventArgs e)
        {

            if (null == this.sensor)
            {
                // Show "No ready Kinect found!" on status bar
                this.KinectStatusText.Content = Properties.Resources.NoReadyKinect;
            }
            else
            {
                // Calculate time span from last calculation of FPS
                double intervalSeconds = (DateTime.Now - this.lastFPSTimestamp).TotalSeconds;

                // Calculate and show fps on status bar
                this.KinectStatusText.Content = string.Format(
                    System.Globalization.CultureInfo.InvariantCulture,
                    Properties.Resources.Fps,
                    (double)this.processedFrameCount / intervalSeconds);
            }

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
                
                // set the RGB image to the RGB camera
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
                double maxDepth = Far_Filter_Slider.Value;
                short[] pixelData = new short[imageFrame.PixelDataLength];
                imageFrame.CopyPixelDataTo(pixelData);
                this.greatestDepth = 0;
                for (int y = 0; y < 240; y++)
                {
                    for (int x = 0; x < 320; x++)
                    {
                        // scale depth down
                        this.Depth[x + (y * 320)] = ((ushort)pixelData[x + y * 320]) / 100;

                        // finds the furthest depth from all the depth pixels
                        if ((this.Depth[x + y * 320] > this.greatestDepth) && (this.Depth[x + y * 320] < maxDepth))
                        {
                            this.greatestDepth = (ushort)this.Depth[x + y * 320];
                        }


                    }
                }
                // Blur Filter  -- Guassian
                if (Filter_Blur.IsChecked == true)
                {
                    for (int i = 641; i < this.Depth.Length - 641; ++i)
                    {

                        short depthaverage = (Int16)((this.Depth[i - 641] + (2 * this.Depth[i - 640]) + this.Depth[i - 639] +
                                                     (2 * this.Depth[i - 1]) + (4 * this.Depth[i]) + (2 * this.Depth[i + 2]) +
                                                     this.Depth[i + 639] + (2 * this.Depth[i + 640]) + this.Depth[i + 641]) / 16);

                        this.Depth[i] = depthaverage;
                        if ((this.Depth[i] > this.greatestDepth) && (this.Depth[i] < maxDepth))
                        {
                            this.greatestDepth = (ushort)this.Depth[i];
                        }
                    }
                }

                // Set the depth image to the Depth sensor view
                this.KinectDepthView.Source = DepthToBitmapSource(imageFrame);
            }
        }


        /// <summary>
        /// Flag check for a point within the bounding box
        /// </summary>
        /// <param name="x">location on the x plane</param>
        /// <param name="y">location on the y plane</param>
        private bool PointinRange(int x, int y)
        {
            double minDepth = Near_Filter_Slider.Value;
            double maxDepth = Far_Filter_Slider.Value;
            return ((this.Depth[x + (y * 320)] >= minDepth && this.Depth[x + (y * 320)] <= maxDepth) ||
                (this.Depth[(x + s) + (y * 320)] >= minDepth && this.Depth[(x + s) + (y * 320)] <= maxDepth) ||
                (this.Depth[x + ((y + s) * 320)] >= minDepth && this.Depth[x + ((y + s) * 320)] <= maxDepth) ||
                (this.Depth[(x + s) + ((y + s) * 320)] >= minDepth && this.Depth[(x + s) + ((y + s) * 320)] <= maxDepth));

        }

        /// <summary>
        /// Create the mesh
        /// </summary>
        void BuildMesh()
        {
            double maxDepth = Far_Filter_Slider.Value;
            int i = 0;
            for (int y = (int)Top_Slider.Value; y < ((int)Bot_Slider.Value - s); y = y + s)
            {
                for (int x = (int)Left_Slider.Value; x < ((int)Right_Slider.Value - s); x = x + s)
                {
                    //Any point less than max
                    if (PointinRange(x, y))
                    {
                        if (this.Depth[x + ((y + s) * 320)] >= maxDepth)
                        {
                            depths_array[0] = -this.greatestDepth;
                        }
                        else
                        {
                            depths_array[0] = -this.Depth[x + ((y + s) * 320)];
                        }

                        if (this.Depth[x + (y * 320)] >= maxDepth)
                        {
                            depths_array[1] = -this.greatestDepth;
                        }
                        else
                        {
                            depths_array[1] = -this.Depth[x + (y * 320)];
                        }

                        if (this.Depth[(x + s) + (y * 320)] >= maxDepth)
                        {
                            depths_array[2] = -this.greatestDepth;
                        }
                        else
                        {
                            depths_array[2] = -this.Depth[(x + s) + (y * 320)];
                        }

                        if (this.Depth[(x + s) + ((y + s) * 320)] >= maxDepth)
                        {
                            depths_array[3] = -this.greatestDepth;
                        }
                        else
                        {
                            depths_array[3] = -this.Depth[(x + s) + ((y + s) * 320)];
                        }

                        // triangle point locations
                        points_array[0] = new Point3D(x, (y + s), depths_array[0]);
                        points_array[1] = new Point3D(x, y, depths_array[1]);
                        points_array[2] = new Point3D((x + s), y, depths_array[2]);
                        points_array[3] = new Point3D((x + s), (y + s), depths_array[3]);

                        // create vectors of size difference between points
                        vectors_array[0] = new Vector3D(points_array[1].X - points_array[0].X, points_array[1].Y - points_array[0].Y, points_array[1].Z - points_array[0].Z);
                        vectors_array[1] = new Vector3D(points_array[1].X - points_array[2].X, points_array[1].Y - points_array[2].Y, points_array[1].Z - points_array[2].Z);
                        vectors_array[2] = new Vector3D(points_array[2].X - points_array[0].X, points_array[2].Y - points_array[0].Y, points_array[2].Z - points_array[0].Z);
                        vectors_array[3] = new Vector3D(points_array[3].X - points_array[0].X, points_array[3].Y - points_array[0].Y, points_array[3].Z - points_array[0].Z);
                        vectors_array[4] = new Vector3D(points_array[2].X - points_array[3].X, points_array[2].Y - points_array[3].Y, points_array[2].Z - points_array[3].Z);

                        // add the corners to the 2 triangles to form a square
                        corners.Add(points_array[0]);
                        corners.Add(points_array[1]);
                        corners.Add(points_array[2]);
                        corners.Add(points_array[2]);
                        corners.Add(points_array[3]);
                        corners.Add(points_array[0]);

                        // add triangles to the collection
                        Triangles.Add(i);
                        Triangles.Add(i + 1);
                        Triangles.Add(i + 2);
                        Triangles.Add(i + 3);
                        Triangles.Add(i + 4);
                        Triangles.Add(i + 5);

                        // find the normals of the triangles by taking the cross product
                        Normals.Add(Vector3D.CrossProduct(vectors_array[0], vectors_array[2]));
                        Normals.Add(Vector3D.CrossProduct(vectors_array[0], vectors_array[1]));
                        Normals.Add(Vector3D.CrossProduct(vectors_array[1], vectors_array[2]));
                        Normals.Add(Vector3D.CrossProduct(vectors_array[1], vectors_array[2]));
                        Normals.Add(Vector3D.CrossProduct(vectors_array[3], vectors_array[4]));
                        Normals.Add(Vector3D.CrossProduct(vectors_array[0], vectors_array[2]));

                        i = i + 6;
                    }

                }
            }

            // add the flat back wall
            int numcorners = corners.Count;
            for (int p = 0; p < numcorners; p++)
            {
                Point3D cornertocopy = corners[p];
                corners.Add(new Point3D(cornertocopy.X, cornertocopy.Y, -this.greatestDepth));
                Triangles.Add(i);
                Normals.Add(new Vector3D(0, 0, 1));
                i = i + 1;
            }


        }

        /// <summary>
        /// Create depth image from depth frame
        /// </summary>
        /// <param name="imageFrame">collection of depth data</param>
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

        /// <summary>
        /// take a photo when button is clicked
        /// </summary>
        /// <param name="sender">object sending the event</param>
        /// <param name="e">event arguments</param>
        private void Begin_Scan_Click(object sender, RoutedEventArgs e)
        {
            //clear the canvas
            this.ClearMesh();

            // add light to the scene
            DirectionalLight DirLight1 = new DirectionalLight();
            DirLight1.Color = Colors.White;
            DirLight1.Direction = new Vector3D(0, 0, -1);

            // add a camera to the scene
            PerspectiveCamera Camera1 = new PerspectiveCamera();

            // set the location of the camera
            Camera1.Position = new Point3D(160, 120, 480);
            Camera1.LookDirection = new Vector3D(0, 0, -1);
            Camera1.UpDirection = new Vector3D(0, -1, 0);

            // create the mesh from depth data
            this.BuildMesh();

            // add texture to all the points
            tmesh.Positions = corners;
            tmesh.TriangleIndices = Triangles;
            tmesh.Normals = Normals;
            tmesh.TextureCoordinates = myTextureCoordinatesCollection;
            msheet.Geometry = tmesh;
            msheet.Material = new DiffuseMaterial((SolidColorBrush)(new BrushConverter().ConvertFrom("#52318F")));

            // build the scene and display it
            this.modelGroup.Children.Add(msheet);
            this.modelGroup.Children.Add(DirLight1);
            this.modelsVisual.Content = this.modelGroup;
            this.myViewport.IsHitTestVisible = false;
            this.myViewport.Camera = Camera1;
            this.myViewport.Children.Add(this.modelsVisual);
            KinectNormalView.Children.Add(this.myViewport);
            this.myViewport.Height = KinectNormalView.Height;
            this.myViewport.Width = KinectNormalView.Width;
            Canvas.SetTop(this.myViewport, 0);
            Canvas.SetLeft(this.myViewport, 0);

        }

        /// <summary>
        /// Export the completed mesh to a .obj file
        /// </summary>
        /// <param name="sender">object sending the event</param>
        /// <param name="e">event arguments</param>
        private void Export_Model_Click(object sender, RoutedEventArgs e)
        {
            //function from Helix Toolkit
            string fileName = Model_Name.Text + ".obj";

            using (var exporter = new ObjExporter(fileName))
            {
                exporter.Export(this.modelGroup);
            }

            // test code for seeing depth frame values
            Process.Start("explorer.exe", "/select,\"" + fileName + "\"");

            string fileName2 = "depth.txt";

            using (System.IO.StreamWriter file = new System.IO.StreamWriter(fileName2))
            {
                //file.Write(string.Join(",", this.Depth));
                file.Write(greatestDepth);
            }

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
                this.KinectStatusText.Content = message;
            }));
        }

        /// <summary>
        /// clear everything from the scene and canvas
        /// </summary>
        public void ClearMesh()
        {
            KinectNormalView.Children.Clear();
            modelGroup.Children.Clear();
            myViewport.Children.Clear();
            modelsVisual.Children.Clear();
            tmesh.Positions.Clear();
            tmesh.TriangleIndices.Clear();
            tmesh.Normals.Clear();
            tmesh.TextureCoordinates.Clear();

        }

        /// <summary>
        /// Clear canvas button click
        /// </summary>
        /// <param name="sender">object sending the event</param>
        /// <param name="e">event arguments</param>
        private void End_Scan_Click(object sender, RoutedEventArgs e)
        {
            this.ClearMesh();
        }
    }
}