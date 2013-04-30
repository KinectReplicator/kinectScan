### Welcome to KinectScan.
This was created to serve as an electronic archive to the work performed during the Spring semester of 2013 for the Rutgers University Electrical & Computer Engineering Senior Capstone Design. 

### Project Abstract
The Microsoft Kinect for Windows has proven to be a valuable tool in the field of computer vision. The Kinect is comprised of an infrared laser projector and depth sensor. The depth data of a scene is run through a bilateral filter and vector mathematics is used to define the coordinates, connecting lines, the vertices, and edges to form a three-dimensional mesh. The software displays the raw depth data and infrared camera image, this allows the user to filter out objects closer or further than a specified depth, and exports the reconstructed three-dimensional mesh. That mesh is then sliced into horizontal layers and converted into G-Code, a machine language that maneuvers the three-dimensional printer where to extrude the ABS plastic to create a physical replica of the reconstructed object. 

### Final Report and Summary Video
The final submitted report can be found here: [KinectScan Final Report](https://github.com/KinectReplicator/Documentation/raw/master/FinalReport.pdf)

A one minute YouTube video that displays how the project works can be found here: [YouTube Video](http://www.youtube.com/watch?v=5dCNUGW-1Hg)

### Authors and Contributors
This project was design, created, and implemented in over 300 hours of work by: Ryan Cullinane (@covertPZ), Cady Motyka (@cmotyka), and Elie Rosen (@erosen). All of which completed their undergraduate degree from the Rutgers Electrical & Computer Engineering Program in May of 2013. 

### Try our Code
To run the program:
1. connect a Microsoft Kinect Device to your personal computer. 
2. Download the project from the links at the top. 
3. Navigate to the "Release" folder. 
4. Run the application "KinectScan.exe".

### KinectScan Instructions
To create a 3-D mesh of the current scene press the "Record Frame" button. To clear a mesh, you may either take another capture or press the "Clear Canvas" button. The sliders may be used to configure how far the Kinect should look back and to create a bounding box to crop a scene. This must be performed before recording a frame. When a desired mesh is captured, fill in the model name box and press "Export Model". The model will export which can then be opened in 3-D software such as [MeshLab](http://meshlab.sourceforge.net/). 

### 3-D Print a Mesh
To print the mesh it must first be ran through [Netfabb](http://cloud.netfabb.com/) to correctly fill in the volume, the output from this web-based application will make sure that the 3-D scan is oriented correctly and manifold. 

Run the new object through a 3-D printer G-code compiler such as [Slic3r](http://slic3r.org/) or [MakerWare](http://www.makerbot.com/makerware/) which will allow for the mesh to be printed on a 3-D printer.

### Enjoy!
Photos from our semester long project can be found here: [SkyDrive Gallery](http://sdrv.ms/13elU5N)