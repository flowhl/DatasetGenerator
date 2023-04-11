using System;
using System.Collections.Generic;
using System.IO;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Runtime.CompilerServices;
using Emgu.CV.Structure;
using Emgu.CV;
using Newtonsoft.Json;
using System.Drawing.Imaging;
using System.Linq;
using System.Diagnostics;
using System.Threading.Tasks;
using System.Threading;

namespace DatasetGenerator
{
    internal class Program
    {
        public static List<string> IconNames = new List<string>();
        public static string[] icons = Directory.GetFiles(@"D:\Github_repos\xstrat\AI\xstrat-ai\Dataset\Icons", "*.png");
        public static string[] backgrounds = Directory.GetFiles(@"D:\Github_repos\xstrat\AI\xstrat-ai\Dataset\Backgrounds", "*.*");
        public static string results = @"D:\Github_repos\xstrat\AI\xstrat-ai\Dataset\Results";

        //COCO
        static CocoInfo info;
        static CocoLicense license;
        static List<CocoImage> images = new List<CocoImage>();
        static object lockImages = new object();
        static List<CocoAnnotation> annotations = new List<CocoAnnotation>();
        static object lockAnnotations = new object();
        static List<CocoCategory> categories = new List<CocoCategory>();


        static void Main(string[] args)
        {
            // Create COCO info
            info = new CocoInfo
            {
                description = "R6 Icons Dataset",
                version = "1.0",
                year = "2023",
                contributor = "Florian Wahl",
                date_created = DateTime.Now.ToString()
            };
            // Create COCO licenses
            license = new CocoLicense
            {
                id = 1,
                name = "License Name",
                url = "http://example.com/license"
            };

            Run().Wait();
            Console.WriteLine("Icons:");
            foreach (var icon in icons)
            {
                Console.WriteLine($"{icons.ToList().IndexOf(icon)}; {Path.GetFileNameWithoutExtension(icon)}");
                categories.Add(new CocoCategory
                {
                    id = icons.ToList().IndexOf(icon) +1,
                    name = Path.GetFileNameWithoutExtension(icon)

                });
            }

            //finish COCO:
            var CocoObject = new CocoAnnotations
            {
                info = info,
                categories = categories,
                annotations = annotations,
                images = images,
                licenses = new List<CocoLicense> { license }
            };

            string json = JsonConvert.SerializeObject(CocoObject);
            File.WriteAllText(Path.Combine(results, "annotations.json"), json);
        }

        public static async Task Run()
        {
            int maxDegreeOfParallelism = Environment.ProcessorCount -2; // Number of CPU threads to use
            long maxMemoryUsage = 8000000000; // Maximum memory usage in bytes (8GB)

            SemaphoreSlim semaphore = new SemaphoreSlim(maxDegreeOfParallelism); // Semaphore to control parallelism

            foreach (var icon in icons)
            {
                IconNames.Add(Path.GetFileNameWithoutExtension(icon));
            }

            Console.WriteLine($"Loaded {backgrounds.Length} backgrounds");


            foreach (var bg in backgrounds)
            {
                await semaphore.WaitAsync(); // Wait for a free slot in the semaphore

                // Check memory usage and wait if necessary
                while (GC.GetTotalMemory(false) > maxMemoryUsage)
                {
                    Console.WriteLine("Waiting for memory");
                    await Task.Delay(100); // Delay for 100ms
                }

                // Start a new Task to process the image asynchronously
                await Task.Run(async () =>
                {
                    try
                    {
                        await GenerateDSForBG(bg); // Call the image editing function asynchronously
                    }
                    finally
                    {
                        semaphore.Release(); // Release the semaphore slot when processing is complete
                    }
                });

                
            }
            // Wait for all image processing tasks to complete
            await semaphore.WaitAsync();
            semaphore.Release(maxDegreeOfParallelism);
            Console.WriteLine("Done");
        }

        public static async Task GenerateDSForBG(string bg)
        {
            Console.WriteLine("Processing: " + bg);
            Random random = new Random();

            Image<Bgr, byte> background = new Image<Bgr, byte>(bg);

            //COCO Image
            var ccImage = new CocoImage
            {
                id = images.Count + 1,
                width = background.Width,
                height = background.Height,
                file_name = Path.GetFileName(bg),
                license = 1,
                date_captured = DateTime.Now.Date.ToString()
            };


            string icon1 = icons[random.Next(icons.Length)];
            string icon2 = icons[random.Next(icons.Length)];
            string icon3 = icons[random.Next(icons.Length)];

            var bgImage = Image.FromFile(bg) as Bitmap;

            Image img = CreateIconOnBG(bgImage, icon1, ccImage);
            Image img2 = CreateIconOnBG(img, icon2, ccImage);
            Image imgFinal = CreateIconOnBG(img2, icon3, ccImage);

            string exportPath = Path.Combine(results, Path.GetFileName(bg));
            imgFinal.Save(exportPath, ImageFormat.Png);

            
            lock (lockImages)
            {
                images.Add(ccImage);
            }
           
            imgFinal.Dispose();
            img.Dispose();
            img2.Dispose();
            bgImage.Dispose();

        }

        public static Image CreateIconOnBG(Image backgroundImage, string iconPath, CocoImage image)
        {
            // Load overlay image
            using (var iconImage = Image.FromFile(iconPath))
            {
                // Generate random scale, rotation, and position values for the icon
                Random random = new Random();
                double scale = random.NextDouble() * (3 - 0.1) + 0.1;
                double angle = random.Next(-30, 30);
                if (backgroundImage.Width <= iconImage.Width || backgroundImage.Height <= iconImage.Height) return backgroundImage;
                Point position = new Point(random.Next((int)(backgroundImage.Width - iconImage.Width)), random.Next((int)(backgroundImage.Height - iconImage.Height)));
                

                // Create a resultImage variable
                var resultImage = new Bitmap(backgroundImage.Width, backgroundImage.Height);

                // Load the backgroundImage in the resultImage
                using (Graphics graphics = Graphics.FromImage(resultImage))
                {
                    graphics.DrawImage((Image)backgroundImage, new Point(0, 0));

                    // Transform and place the iconImage on the backgroundImage
                    //using (var transformedIconImage = RotateImage(iconImage, (float)angle))
                    using (var transformedIconImage = iconImage)
                    {
                        using (var scaledIconImage = ScaleImage(transformedIconImage, scale))
                        {
                            // Create a matrix transform for the rotation and scaling
                            Matrix matrix = new Matrix();
                            matrix.RotateAt((float)angle, new PointF(position.X + scaledIconImage.Width / 2, position.Y + scaledIconImage.Height / 2));
                            matrix.Scale((float)scale, (float)scale, MatrixOrder.Append);
                            //graphics.Transform = matrix;

                            graphics.DrawImage(scaledIconImage, position);

                            // Calculate the bounding box of the transformed iconImage
                            PointF[] points = new PointF[]
                            {
                                new PointF(0, 0),
                                new PointF(scaledIconImage.Width, 0),
                                new PointF(scaledIconImage.Width, scaledIconImage.Height),
                                new PointF(0, scaledIconImage.Height)
                            };
                            matrix.TransformPoints(points);
                            float minX = points.Min(p => p.X);
                            float minY = points.Min(p => p.Y);
                            float maxX = points.Max(p => p.X);
                            float maxY = points.Max(p => p.Y);
                            RectangleF boundingBox = new RectangleF(minX + position.X, minY + position.Y, maxX - minX, maxY - minY);
                            RectangleF outlines = new RectangleF(position.X, position.Y, scaledIconImage.Width, scaledIconImage.Height);

                            lock(lockAnnotations)
                            {
                                annotations.Add(new CocoAnnotation
                                {
                                    id = annotations.Count() + 1,
                                    image_id = image.id,
                                    category_id = icons.ToList().IndexOf(iconPath) +1,
                                    segmentation = new List<List<double>> { new List<double> { outlines.Left, outlines.Top, outlines.Right, outlines.Top, outlines.Right, outlines.Bottom, outlines.Left, outlines.Bottom } },
                                    area = outlines.Width * outlines.Height,
                                    bbox = new List<double> { outlines.Left, outlines.Top, outlines.Width, outlines.Height },
                                    iscrowd = 0
                                });
                            }

                            // Reset the graphics transform
                            graphics.ResetTransform();

                            // Save the resultImage to the exportPath
                            return resultImage;
                        }
                    }
                }

            }

        }



        public static void OverlayImages(string baseImagePath, string overlayImagePath, string outputImagePath, int overlayWidth, int overlayHeight, float rotationDegrees, int overlayX, int overlayY)
        {

        }

        // Helper function to rotate an image
        private static Image RotateImage(Image image, float angle)
        {
            Bitmap rotatedImage = new Bitmap(image.Width, image.Height);
            using (Graphics graphics = Graphics.FromImage(rotatedImage))
            {
                graphics.TranslateTransform((float)image.Width / 2, (float)image.Height / 2);
                graphics.RotateTransform(angle);
                graphics.TranslateTransform(-(float)image.Width / 2, -(float)image.Height / 2);
                graphics.DrawImage(image, new Point(0, 0));
            }
            return rotatedImage;
        }

        // Helper function to scale an image
        private static Image ScaleImage(Image image, double scale)
        {
            int newWidth = (int)(image.Width * scale);
            int newHeight = (int)(image.Height * scale);
            Bitmap scaledImage = new Bitmap(newWidth, newHeight);
            using (Graphics graphics = Graphics.FromImage(scaledImage))
            {
                graphics.DrawImage(image, new Rectangle(0, 0, newWidth, newHeight));
            }
            return scaledImage;
        }
    }

    #region COCO
    // Define the COCO annotation classes
    public class CocoInfo
    {
        public string description;
        public string url;
        public string version;
        public string year;
        public string contributor;
        public string date_created;
    }

    public class CocoLicense
    {
        public int id;
        public string name;
        public string url;
    }

    public class CocoImage
    {
        public int id;
        public int width;
        public int height;
        public string file_name;
        public int license;
        public string flickr_url;
        public string coco_url;
        public string date_captured;
    }

    public class CocoAnnotation
    {
        public int id;
        public int image_id;
        public int category_id;
        public List<List<double>> segmentation;
        public double area;
        public List<double> bbox;
        public int iscrowd;
    }

    public class CocoCategory
    {
        public int id;
        public string name;
        public string supercategory;
    }

    public class CocoAnnotations
    {
        public CocoInfo info;
        public List<CocoLicense> licenses;
        public List<CocoImage> images;
        public List<CocoAnnotation> annotations;
        public List<CocoCategory> categories;
    }

    #endregion

}
