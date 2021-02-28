using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;

using System.Windows.Media.Media3D;
using System.IO;

namespace howto_sample_altitude_map
{
    /// <summary>
    /// Interaction logic for Window1.xaml
    /// </summary>
    public partial class Window1 : Window
    {
        public Window1()
        {
            InitializeComponent();
        }

        // The main object model group.
        private Model3DGroup MainModel3Dgroup = new Model3DGroup();

        // The camera.
        private PerspectiveCamera TheCamera;

        // The camera's current location.
        private double CameraPhi = Math.PI / 6.0;       // 30 degrees
        private double CameraTheta = Math.PI / 6.0;     // 30 degrees
        private double CameraR = 17.0;

        // The change in CameraPhi when you press the up and down arrows.
        private const double CameraDPhi = 0.1;

        // The change in CameraTheta when you press the left and right arrows.
        private const double CameraDTheta = 0.1;

        // The change in CameraR when you press + or -.
        private const double CameraDR = 0.1;

        // Create the scene.
        // MainViewport is the Viewport3D defined
        // in the XAML code that displays everything.
        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            // Give the camera its initial position.
            TheCamera = new PerspectiveCamera();
            TheCamera.FieldOfView = 60;
            MainViewport.Camera = TheCamera;
            PositionCamera();

            // Define lights.
            DefineLights();

            // Create the data.
            double[,] values = MakeData();

            // Create an altitude map.
            CreateAltitudeMap(values);

            // Create the model.
            DefineModel(MainModel3Dgroup, values);

            // Add the group of models to a ModelVisual3D.
            ModelVisual3D model_visual = new ModelVisual3D();
            model_visual.Content = MainModel3Dgroup;

            // Display the main visual to the viewportt.
            MainViewport.Children.Add(model_visual);
        }

        // Define the lights.
        private void DefineLights()
        {
            AmbientLight ambient_light = new AmbientLight(Colors.Gray);
            DirectionalLight directional_light =
                new DirectionalLight(Colors.Gray, new Vector3D(-1.0, -3.0, -2.0));
            MainModel3Dgroup.Children.Add(ambient_light);
            MainModel3Dgroup.Children.Add(directional_light);
        }

        private int xmin, xmax, dx, zmin, zmax, dz;
        private double texture_xscale, texture_zscale;

        // Make the data.
        private double[,] MakeData()
        {
            double[,] values =
            {
                {0,0,0,1,2,2,1,0,0,0},
                {0,0,2,3,3,3,3,2,0,0},
                {0,2,3,4,4,4,4,3,2,0},
                {2,3,4,5,5,5,5,4,3,2},
                {3,4,5,6,7,7,6,5,4,3},
                {3,4,5,6,7,7,6,5,4,3},
                {2,3,4,5,5,5,5,4,3,2},
                {0,2,3,4,4,4,4,3,2,0},
                {0,0,2,3,3,3,3,2,0,0},
                {0,0,0,1,2,2,1,0,0,0}
            };

            xmin = 0;
            xmax = values.GetUpperBound(0);
            dx = 1;
            zmin = 0;
            zmax = values.GetUpperBound(1);
            dz = 1;

            texture_xscale = (xmax - xmin);
            texture_zscale = (zmax - zmin);

            return values;
        }

        // Create the altitude map texture bitmap.
        private void CreateAltitudeMap(double[,] values)
        {
            // Calculate the function's value over the area.
            int xwidth = values.GetUpperBound(0) + 1;
            int zwidth = values.GetUpperBound(1) + 1;
            double dx = (xmax - xmin) / xwidth;
            double dz = (zmax - zmin) / zwidth;

            // Get the upper and lower bounds on the values.
            var get_values =
                from double value in values
                select value;
            double ymin = get_values.Min();
            double ymax = get_values.Max();

            // Make the BitmapPixelMaker.
            BitmapPixelMaker bm_maker = new BitmapPixelMaker(xwidth, zwidth);

            // Set the pixel colors.
            for (int ix = 0; ix < xwidth; ix++)
            {
                for (int iz = 0; iz < zwidth; iz++)
                {
                    byte red, green, blue;
                    MapRainbowColor(values[ix, iz], ymin, ymax,
                        out red, out green, out blue);
                    bm_maker.SetPixel(ix, iz, red, green, blue, 255);
                }
            }

            // Convert the BitmapPixelMaker into a WriteableBitmap.
            WriteableBitmap wbitmap = bm_maker.MakeBitmap(96, 96);

            // Save the bitmap into a file.
            wbitmap.Save("Texture.png");
        }

        // Map a value to a rainbow color.
        private void MapRainbowColor(double value, double min_value, double max_value,
            out byte red, out byte green, out byte blue)
        {
            // Convert into a value between 0 and 1023.
            int int_value = (int)(1023 * (value - min_value) / (max_value - min_value));

            // Map different color bands.
            if (int_value < 256)
            {
                // Red to yellow. (255, 0, 0) to (255, 255, 0).
                red = 255;
                green = (byte)int_value;
                blue = 0;
            }
            else if (int_value < 512)
            {
                // Yellow to green. (255, 255, 0) to (0, 255, 0).
                int_value -= 256;
                red = (byte)(255 - int_value);
                green = 255;
                blue = 0;
            }
            else if (int_value < 768)
            {
                // Green to aqua. (0, 255, 0) to (0, 255, 255).
                int_value -= 512;
                red = 0;
                green = 255;
                blue = (byte)int_value;
            }
            else
            {
                // Aqua to blue. (0, 255, 255) to (0, 0, 255).
                int_value -= 768;
                red = 0;
                green = (byte)(255 - int_value);
                blue = 255;
            }
        }

        // Add the model to the Model3DGroup.
        private void DefineModel(Model3DGroup model_group, double[,] values)
        {
            // Make a mesh to hold the surface.
            MeshGeometry3D mesh = new MeshGeometry3D();

            // Make the surface's points and triangles.
            float offset_x = xmax / 2f;
            float offset_z = zmax / 2f;
            for (int x = xmin; x <= xmax - dx; x += dx)
            {
                for (int z = zmin; z <= zmax - dz; z += dx)
                {
                    // Make points at the corners of the surface
                    // over (x, z) - (x + dx, z + dz).
                    Point3D p00 = new Point3D(x - offset_x, values[x, z], z - offset_z);
                    Point3D p10 = new Point3D(x - offset_x + dx, values[x + dx, z], z - offset_z);
                    Point3D p01 = new Point3D(x - offset_x, values[x, z + dz], z - offset_z + dz);
                    Point3D p11 = new Point3D(x - offset_x + dx, values[x + dx, z + dz], z - offset_z + dz);

                    // Add the triangles.
                    AddTriangle(mesh, p00, p01, p11);
                    AddTriangle(mesh, p00, p11, p10);
                }
            }
            Console.WriteLine(mesh.Positions.Count + " points");
            Console.WriteLine(mesh.TriangleIndices.Count / 3 + " triangles");
            Console.WriteLine();

            // Make the surface's material using an image brush.
            ImageBrush texture_brush = new ImageBrush();
            texture_brush.ImageSource =
                new BitmapImage(new Uri("Texture.png", UriKind.Relative));
            DiffuseMaterial surface_material = new DiffuseMaterial(texture_brush);

            // Make the mesh's model.
            GeometryModel3D surface_model = new GeometryModel3D(mesh, surface_material);

            // Make the surface visible from both sides.
            surface_model.BackMaterial = surface_material;

            // Add the model to the model groups.
            model_group.Children.Add(surface_model);
        }

        // Add a triangle to the indicated mesh.
        // If the triangle's points already exist, reuse them.
        private void AddTriangle(MeshGeometry3D mesh, Point3D point1, Point3D point2, Point3D point3)
        {
            // Get the points' indices.
            int index1 = AddPoint(mesh.Positions, mesh.TextureCoordinates, point1);
            int index2 = AddPoint(mesh.Positions, mesh.TextureCoordinates, point2);
            int index3 = AddPoint(mesh.Positions, mesh.TextureCoordinates, point3);

            // Create the triangle.
            mesh.TriangleIndices.Add(index1);
            mesh.TriangleIndices.Add(index2);
            mesh.TriangleIndices.Add(index3);
        }

        // A dictionary to hold points for fast lookup.
        private Dictionary<Point3D, int> PointDictionary =
            new Dictionary<Point3D, int>();

        // If the point already exists, return its index.
        // Otherwise create the point and return its new index.
        private int AddPoint(Point3DCollection points,
            PointCollection texture_coords, Point3D point)
        {
            // If the point is in the point dictionary,
            // return its saved index.
            if (PointDictionary.ContainsKey(point))
                return PointDictionary[point];

            // We didn't find the point. Create it.
            points.Add(point);
            PointDictionary.Add(point, points.Count - 1);

            // Set the point's texture coordinates.
            texture_coords.Add(
                new Point(
                    (point.X - xmin) * texture_xscale,
                    (point.Z - zmin) * texture_zscale));

            // Return the new point's index.
            return points.Count - 1;
        }

        // Adjust the camera's position.
        private void Window_KeyDown(object sender, KeyEventArgs e)
        {
            switch (e.Key)
            {
                case Key.Up:
                    CameraPhi += CameraDPhi;
                    if (CameraPhi > Math.PI / 2.0) CameraPhi = Math.PI / 2.0;
                    break;
                case Key.Down:
                    CameraPhi -= CameraDPhi;
                    if (CameraPhi < -Math.PI / 2.0) CameraPhi = -Math.PI / 2.0;
                    break;
                case Key.Left:
                    CameraTheta += CameraDTheta;
                    break;
                case Key.Right:
                    CameraTheta -= CameraDTheta;
                    break;
                case Key.Add:
                case Key.OemPlus:
                    CameraR -= CameraDR;
                    if (CameraR < CameraDR) CameraR = CameraDR;
                    break;
                case Key.Subtract:
                case Key.OemMinus:
                    CameraR += CameraDR;
                    break;
            }

            // Update the camera's position.
            PositionCamera();
        }

        // Position the camera.
        private void PositionCamera()
        {
            // Calculate the camera's position in Cartesian coordinates.
            double y = CameraR * Math.Sin(CameraPhi);
            double hyp = CameraR * Math.Cos(CameraPhi);
            double x = hyp * Math.Cos(CameraTheta);
            double z = hyp * Math.Sin(CameraTheta);
            TheCamera.Position = new Point3D(x, y, z);

            // Look toward the origin.
            TheCamera.LookDirection = new Vector3D(-x, -y, -z);

            // Set the Up direction.
            TheCamera.UpDirection = new Vector3D(0, 1, 0);

            // Console.WriteLine("Camera.Position: (" + x + ", " + y + ", " + z + ")");
        }
    }
}
