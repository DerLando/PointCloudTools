using System;
using System.Drawing;
using Rhino;
using Rhino.Commands;
using Rhino.DocObjects;
using Rhino.Geometry;
using Rhino.Input.Custom;
using Rhino.Render;
using System.Xml;

namespace PointCloudTools.Commands
{
    public class PcSamplePicture : Command
    {
        static PcSamplePicture _instance;
        public PcSamplePicture()
        {
            _instance = this;
        }

        ///<summary>The only instance of the PcSamplePicture command.</summary>
        public static PcSamplePicture Instance
        {
            get { return _instance; }
        }

        public override string EnglishName
        {
            get { return "PcSamplePicture"; }
        }

        protected override Result RunCommand(RhinoDoc doc, RunMode mode)
        {
            // TODO: complete command.

            // get picture object
            ObjRef objRef = null;
            using (var go = new GetObject())
            {
                go.SetCommandPrompt("Select a picture object to sample");
                go.GeometryFilter = ObjectType.Surface;

                go.Get();
                if (go.CommandResult() != Result.Success) return go.CommandResult();

                objRef = go.Object(0);
            }

            // get rhino object
            RhinoObject rhinoObject = objRef.Object();

            // extract bitmap texture from render material Xml
            RenderMaterial renderMaterial = rhinoObject.RenderMaterial;
            XmlDocument xml = new XmlDocument();
            xml.LoadXml(renderMaterial.Xml);
            var node = xml.SelectSingleNode("material/simulation/Texture-1-filename");
            var filePath = node.InnerText;
            var bitmap = new Bitmap(Image.FromFile(filePath));

            // get bounding box
            var bbox = rhinoObject.Geometry.GetBoundingBox(Plane.WorldXY, out var worldBox);

            // get width / height of bbox
            double bboxWidth = bbox.Corner(false, true, true).DistanceTo(bbox.Corner(true, true, true));
            double bboxHeight = bbox.Corner(true, false, true).DistanceTo(bbox.Corner(true, true, true));

            // get width / height of image
            int imgWidth = bitmap.Width;
            int imgHeight = bitmap.Height;

            // Calculate stretch factor
            double stretchFactor = bboxWidth / imgWidth;

            // Test stretch factor consistency
            if(!(Math.Abs(imgHeight * stretchFactor - bboxHeight) < doc.ModelAbsoluteTolerance))
            {
                RhinoApp.WriteLine("Selected Picture object is scaled non-uniform! Aborting command");
                return Result.Failure;
            }

            // get starting position
            var startPt = bbox.Min;

            // create grid of points
            var plane = worldBox.Plane;
            plane.Origin = startPt;
            Point3d[] points = new Point3d[imgWidth * imgHeight];
            Color[] colors = new Color[imgWidth * imgHeight];
            Vector3d[] normals = new Vector3d[imgWidth * imgHeight];
            Vector3d normal = Vector3d.ZAxis;
            for (int i = 0; i < imgHeight; i++)
            {
                for (int j = 0; j < imgWidth; j++)
                {
                    points[(i + 1) * j] = plane.PointAt(j * stretchFactor, i * stretchFactor);
                    colors[(i + 1) * j] = bitmap.GetPixel(j, i);
                    normals[(i + 1) * j] = normal;
                }
            }

            // create Point cloud from grid
            PointCloud pc = new PointCloud();
            pc.AddRange(points, normals, colors);

            // add point cloud to doc
            doc.Objects.AddPointCloud(pc);

            // redraw
            doc.Views.Redraw();

            // return success
            return Result.Success;
        }
    }
}