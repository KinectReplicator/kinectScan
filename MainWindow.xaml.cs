﻿
namespace kinectScan
{
    using System;
    using System.IO;
    using System.Drawing;
    using System.Diagnostics;
    using System.Windows;
    using System.Windows.Controls;
    using System.Windows.Media;
    using System.Windows.Media.Imaging;
    using System.Windows.Media.Media3D;

    using HelixToolkit.Wpf;

    using Microsoft.Kinect;
    
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
        GeometryModel3D[] points = new GeometryModel3D[320 * 240];

        Model3DGroup modelGroup = new Model3DGroup();

        int s = 2;

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

                myViewport.Height = KinectNormalView.Height;
                myViewport.Width = KinectNormalView.Width;
                Canvas.SetTop(myViewport, 0);
                Canvas.SetLeft(myViewport, 0);
                
                // Add an event handler to be called whenever there is new depth frame data available
                this.sensor.DepthFrameReady += this.SensorDepthFrameReady;
               // this.sensor.DepthFrameReady += this.Bilateral_Filter;

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
            // Stop the sensor!
            if (null != this.sensor)
            {
                this.sensor.Stop();
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

            Int32Collection Triangles = new Int32Collection();
            Triangles.Add(0);
            Triangles.Add(1);
            Triangles.Add(2);

            MeshGeometry3D tmesh = new MeshGeometry3D();
            tmesh.Positions = corners;
            tmesh.TriangleIndices = Triangles;
            tmesh.Normals.Add(new Vector3D(0, 0, -1));

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

        private void Export_Model_Click(object sender, RoutedEventArgs e)
        {

            Process.Start("C:\\Users\\" + Environment.UserName + "\\Dropbox\\Capstone\\Slic3r\\slic3r-console.exe", "C:\\Users\\" + Environment.UserName + "\\Dropbox\\Capstone\\Slic3r\\objs\\Old_Key.obj $s");
            Process.Start("explorer.exe", string.Format("/select, \"{0}\"", "C:\\Users\\"+ Environment.UserName + "\\Dropbox\\Capstone\\Slic3r\\objs\\Old_Key.obj.gcode"));
        }

        private void test_Click(object sender, RoutedEventArgs e)
        {
            string fileName = this.Model_Name.Text;
            using (var exporter = new ObjExporter(fileName))
            {
                exporter.Export(this.modelGroup);
            }

            Process.Start("explorer.exe", "/select,\"" + fileName + "\"");
        }

        private void Begin_Scan_Click(object sender, RoutedEventArgs e)
        {

        }
    }
}
