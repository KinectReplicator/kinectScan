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

    //using HelixToolkit.Wpf;

    using Microsoft.Kinect;
    //using Microsoft.Kinect.Toolkit;
    //using Microsoft.Kinect.Toolkit.Fusion;

    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        /// <summary>
        /// Active Kinect sensor
        /// </summary>
        private KinectSensor sensor;

        /// <summary>
        /// Storage for 3D model
        /// </summary>
        /// 

        // Declare scene objects.
        //GeometryModel3D[] points = new GeometryModel3D[320 * 240];
        //Viewport3D myViewport3D = new Viewport3D();
        //Model3DGroup myModel3DGroup = new Model3DGroup();
        //GeometryModel3D myGeometryModel = new GeometryModel3D();
        //ModelVisual3D myModelVisual3D = new ModelVisual3D();
        //MeshGeometry3D myMeshGeometry3D = new MeshGeometry3D();
        //Vector3DCollection myNormalCollection = new Vector3DCollection();
        //Point3DCollection myPositionCollection = new Point3DCollection();
        //Int32Collection myTriangleIndicesCollection = new Int32Collection();
        
        GeometryModel3D[] points = new GeometryModel3D[2*(320 * 240)];
        public int[] Depth = new int[320 * 240];
        Model3DGroup modelGroup = new Model3DGroup();
        public int s=4;
        public int Top_or_bottom = 0;

        public byte[] colorPixels;
        public WriteableBitmap colorBitmap;

        public MainWindow()
        {
            InitializeComponent();
        }

        private void WindowLoaded(object sender, RoutedEventArgs e)
        {

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
                this.sensor.ColorStream.Enable(ColorImageFormat.InfraredResolution640x480Fps30);

                // Allocate space to put the pixels we'll receive
                this.colorPixels = new byte[this.sensor.ColorStream.FramePixelDataLength];

                // This is the bitmap we'll display on-screen
                this.colorBitmap = new WriteableBitmap(this.sensor.ColorStream.FrameWidth, this.sensor.ColorStream.FrameHeight, 96.0, 96.0, PixelFormats.Gray16, null);

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
                

                //// Defines the camera used to view the 3D object. In order to view the 3D object,
                //// the camera must be positioned and pointed such that the object is within view 
                //// of the camera.
                //PerspectiveCamera myPCamera = new PerspectiveCamera();

                //// Specify where in the 3D scene the camera is.
                //myPCamera.Position = new Point3D(160, 120, -1000);

                //// Specify the direction that the camera is pointing.
                //myPCamera.LookDirection = new Vector3D(0, 0, 1);

                //// Define camera's horizontal field of view in degrees.
                //myPCamera.FieldOfView = 100;

                //// Asign the camera to the viewport
                //myViewport3D.Camera = myPCamera;
                //// Define the lights cast in the scene. Without light, the 3D object cannot 
                //// be seen. Note: to illuminate an object from additional directions, create 
                //// additional lights.
                //DirectionalLight myDirectionalLight = new DirectionalLight();
                //myDirectionalLight.Color = Colors.White;
                //myDirectionalLight.Direction = new Vector3D(1, 1, 1);

                int i = 0;
                for (int y = s; y < (240-s); y = y + s)
                {
                    for (int x = s; x < (320-s); x = x + s)
                    {

                        points[i] = TopTriangle(x, y, s);
                        points[i+1] = BottomTriangle(x, y, s);

                        int Top_X =((this.Depth[x + (y * 320)]) - (this.Depth[x + ((y + s) * 320)])+(this.Depth[(x + s) + (y * 320)]) - (this.Depth[(x + s) + ((y + s) * 320)])+(this.Depth[x + (y * 320)]) - (this.Depth[(x + s) + (y * 320)]))/3;
                        int Top_Y=((this.Depth[(x + s) + ((y + s) * 320)]) - (this.Depth[x + ((y + s) * 320)])+(this.Depth[x + ((y + s) * 320)]) - (this.Depth[(x + s) + ((y + s) * 320)])+(this.Depth[(x + s) + ((y + s) * 320)]) - (this.Depth[(x + s) + (y * 320)]))/3;
                        points[i].Transform = new TranslateTransform3D(Top_X, Top_Y, 1);

                        int Bot_X=((this.Depth[x + (y * 320)]) - (this.Depth[(x + s) + (y * 320)])+ (this.Depth[x + ((y + s) * 320)]) - (this.Depth[x + (y * 320)])+ (this.Depth[x + (y * 320)]) - (this.Depth[x + ((y + s) * 320)]))/3;
                        int Bot_Y=((this.Depth[(x + s) + ((y + s) * 320)]) - (this.Depth[(x + s) + (y * 320)])+(this.Depth[(x + s) + (y * 320)]) - (this.Depth[x + (y * 320)])+(this.Depth[(x + s) + ((y + s) * 320)]) - (this.Depth[x + ((y + s) * 320)]) )/3;
                        points[i+1].Transform = new TranslateTransform3D(Bot_X, Bot_Y, 1);
                        
                        this.modelGroup.Children.Add(points[i]);
                        this.modelGroup.Children.Add(points[i+1]);
                        i=i+2;

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



                //myMeshGeometry3D.Normals = myNormalCollection;
                //myMeshGeometry3D.Positions = myPositionCollection;
                //myMeshGeometry3D.TriangleIndices = myTriangleIndicesCollection;

                //myGeometryModel.Geometry = myMeshGeometry3D;

                //myGeometryModel.Material = new DiffuseMaterial((SolidColorBrush)(new BrushConverter().ConvertFrom("#52318F")));

                //myModel3DGroup.Children.Add(myGeometryModel);
                //myModel3DGroup.Children.Add(myDirectionalLight);
                //myModelVisual3D.Content = myModel3DGroup;

                //LinearGradientBrush myHorizontalGradient = new LinearGradientBrush();
                //myHorizontalGradient.GradientStops.Add(new GradientStop(Colors.Yellow, 0.0));
                //myHorizontalGradient.GradientStops.Add(new GradientStop(Colors.Red, 0.25));
                //myHorizontalGradient.GradientStops.Add(new GradientStop(Colors.Blue, 0.75));
                //myHorizontalGradient.GradientStops.Add(new GradientStop(Colors.LimeGreen, 1.0));

                //// Define material and apply to the mesh geometries.
                //DiffuseMaterial myMaterial = new DiffuseMaterial(myHorizontalGradient);
                //myGeometryModel.Material = myMaterial;

                //myViewport3D.IsHitTestVisible = false;
                //myViewport3D.Children.Add(myModelVisual3D);
                //KinectNormalView.Children.Add(myViewport3D);

                //this.test_text.Text = this.modelGroup.ToString();
                myViewport.Height = KinectNormalView.Height;
                myViewport.Width = KinectNormalView.Width;
                Canvas.SetTop(myViewport, 0);
                Canvas.SetLeft(myViewport, 0);

                // Add an event handler to be called whenever there is new depth frame data available
                this.sensor.DepthFrameReady += this.SensorDepthFrameReady;
                this.sensor.ColorFrameReady += this.SensorColorFrameReady;

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
                this.RGB_Title.Content = "";
            }
        }

        /// <summary>
        /// Execute shutdown tasks
        /// </summary>
        /// <param name="sender">object sending the event</param>
        /// <param name="e">event arguments</param>
        private void WindowClosing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            // Stop the sensor!
            if (null != this.sensor)
            {
                this.sensor.Stop();
            }
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
                        ((TranslateTransform3D)points[i].Transform).OffsetZ = ((this.Depth[x + ((y+s) * 320)]) + (this.Depth[(x+s) + ((y+s) * 320)])+(this.Depth[(x+s) + (y * 320)]))/3;
                        ((TranslateTransform3D)points[i + 1].Transform).OffsetZ = ((this.Depth[(x+s) + (y * 320)]) + (this.Depth[x  + (y  * 320)]) + (this.Depth[x  + ((y+s) * 320)]))/3;
                        i=i+2;

                    }
                }

                this.KinectDepthView.Source = DepthToBitmapSource(imageFrame);
            }
        }
       
        private GeometryModel3D TopTriangle(int x, int y, int s)
        {
            int i = 0;
            Point3DCollection corners = new Point3DCollection();
            corners.Add(new Point3D(x, y + s, this.Depth[x + ((y + s) * 320)]));
            corners.Add(new Point3D(x + s, y + s, this.Depth[(x + s) + ((y + s) * 320)]));
            corners.Add(new Point3D(x + s, y, this.Depth[(x + s) + (y * 320)]));
            
            Int32Collection Triangles = new Int32Collection();
            Triangles.Add(i);
            Triangles.Add(i + 1);
            Triangles.Add(i + 2);
            i = i + 3;

            MeshGeometry3D tmesh = new MeshGeometry3D();
            tmesh.Positions = corners;
            tmesh.TriangleIndices = Triangles;
            tmesh.Normals.Add(new Vector3D((this.Depth[x + (y * 320)]) - (this.Depth[x + ((y + s) * 320)]), (this.Depth[(x + s) + ((y + s) * 320)]) - (this.Depth[x + ((y + s) * 320)]), 1));
            tmesh.Normals.Add(new Vector3D((this.Depth[(x + s) + (y * 320)]) - (this.Depth[(x + s) + ((y + s) * 320)]), (this.Depth[x + ((y + s) * 320)]) - (this.Depth[(x + s) + ((y + s) * 320)]), 1));
            tmesh.Normals.Add(new Vector3D((this.Depth[x + (y * 320)]) - (this.Depth[(x + s) + (y * 320)]), (this.Depth[(x + s) + ((y + s) * 320)]) - (this.Depth[(x + s) + (y * 320)]), 1));

            GeometryModel3D msheet = new GeometryModel3D();
            msheet.Geometry = tmesh;
            msheet.Material = new DiffuseMaterial((SolidColorBrush)(new BrushConverter().ConvertFrom("#52318F")));
            return msheet;
        }

        private GeometryModel3D BottomTriangle(int x, int y, int s)
        {
            int i = 0;
            Point3DCollection corners = new Point3DCollection();
            corners.Add(new Point3D(x + s, y, this.Depth[(x + s) + (y * 320)]));
            corners.Add(new Point3D(x, y, this.Depth[x + (y * 320)]));
            corners.Add(new Point3D(x, y + s, this.Depth[x + ((y + s) * 320)]));

            Int32Collection Triangles = new Int32Collection();
            Triangles.Add(i);
            Triangles.Add(i + 1);
            Triangles.Add(i + 2);
            i = i + 3;

            MeshGeometry3D tmesh = new MeshGeometry3D();
            tmesh.Positions = corners;
            tmesh.TriangleIndices = Triangles;
            tmesh.Normals.Add(new Vector3D((this.Depth[x + (y * 320)]) - (this.Depth[(x + s) + (y * 320)]), (this.Depth[(x + s) + ((y + s) * 320)]) - (this.Depth[(x + s) + (y * 320)]), 1));
            tmesh.Normals.Add(new Vector3D((this.Depth[x + ((y + s) * 320)]) - (this.Depth[x + (y * 320)]), (this.Depth[(x + s) + (y * 320)]) - (this.Depth[x + (y * 320)]), 1));
            tmesh.Normals.Add(new Vector3D((this.Depth[x + (y * 320)]) - (this.Depth[x + ((y + s) * 320)]), (this.Depth[(x + s) + ((y + s) * 320)]) - (this.Depth[x + ((y + s) * 320)]), 1));

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
            return;
        }
        private void test_Click(object sender, RoutedEventArgs e)
        {
            return;
        }

        private void Export_Model_Click(object sender, RoutedEventArgs e)
        {
            //string fileName = "model.obj";
            return;
            //using (var exporter = new ObjExporter(fileName))
            //{
            //    exporter.Export(this.myModel3DGroup);
            //}

            //Process.Start("explorer.exe", "/select,\"" + fileName + "\"");

        }
    }
}