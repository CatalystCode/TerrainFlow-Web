using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;
using BitMiracle.LibTiff.Classic;

namespace GeoTiffSharp
{
    public class GeoTiff : IHeightMapConverter, IDisposable
    {
        Tiff _tiff;

        protected virtual void Dispose(bool disposing)
        {
            if (disposing)
            {
                _tiff?.Dispose();
            }
        }

        ~GeoTiff()
        {
            Dispose(false);
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        private FileMetadata ParseMetadata(string filename)
        {
            FileMetadata metadata = new FileMetadata();
            _tiff = Tiff.Open(filename, "r");

            metadata.Height = _tiff.GetField(TiffTag.IMAGELENGTH)[0].ToInt();
            metadata.Width = _tiff.GetField(TiffTag.IMAGEWIDTH)[0].ToInt();

            FieldValue[] modelPixelScaleTag = _tiff.GetField((TiffTag)33550);
            FieldValue[] modelTiepointTag = _tiff.GetField((TiffTag)33922);

            byte[] modelPixelScale = modelPixelScaleTag[1].GetBytes();
            metadata.PixelScaleX = BitConverter.ToDouble(modelPixelScale, 0);
            metadata.PixelScaleY = BitConverter.ToDouble(modelPixelScale, 8);

            // Ignores first set of model points (3 bytes) and assumes they are 0's...
            byte[] modelTransformation = modelTiepointTag[1].GetBytes();
            metadata.OriginLongitude = BitConverter.ToDouble(modelTransformation, 24);
            metadata.OriginLatitude = BitConverter.ToDouble(modelTransformation, 32);

            // Grab some raster metadata
            metadata.BitsPerSample = _tiff.GetField(TiffTag.BITSPERSAMPLE)[0].ToInt();

            // Add other information about the data
            metadata.SampleFormat = "Single";
            // TODO: Read this from tiff metadata or determine after parsing
            metadata.NoDataValue = "-10000";

            metadata.WorldUnits = "meter";

            return metadata;
        }

        private static void PrintTagInfo(Tiff tiff, TiffTag tiffTag)
        {
            try
            {
                var field = tiff.GetField(tiffTag);
                if (field != null)
                {
                    Console.WriteLine($"{tiffTag}");
                    for (int i = 0; i < field.Length; i++)
                    {
                        Console.WriteLine($"  [{i}] {field[i].Value}");
                        byte[] bytes = field[i].Value as byte[];
                        if (bytes != null)
                        {
                            Console.WriteLine($"    Length: {bytes.Length}");
                            if (bytes.Length % 8 == 0)
                            {
                                for (int k = 0; k < bytes.Length / 8; k++)
                                {
                                    Console.WriteLine($"      [{k}] {BitConverter.ToDouble(bytes, k * 8)}");
                                }
                            }

                            try
                            {
                                Console.WriteLine($"   > {System.Text.Encoding.ASCII.GetString(bytes).Trim()} < ");
                            }
                            catch (Exception ex)
                            {

                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"ERROR: {tiffTag}");
            }
        }

        private float[,] WriteBinary(string inputFilename, string bitmapFilename, FileMetadata metadata)
        {
            float min = float.MaxValue;
            float max = float.MinValue;
            float range;

            int width, height, increment;

            if (metadata.Width > 0x1fff || metadata.Height > 0x1fff)
            {
                width = metadata.Width / 2;
                height = metadata.Height / 2;
                increment = 2;
            }
            else
            {
                width = metadata.Width;
                height = metadata.Height;
                increment = 1;
            }

            float[,] data = new float[width, height];

            for (int i = 0; i <= metadata.Height - increment; i += increment)
            {
                byte[] buffer = new byte[metadata.Width * metadata.BitsPerSample / 8];
                _tiff.ReadScanline(buffer, i);
                for (int p = 0; p <= metadata.Width - increment; p += increment)
                {
                    var heightValue = BitConverter.ToSingle(buffer, p * metadata.BitsPerSample / 8);
                    data[p/increment, i/increment] = heightValue;
                    if (heightValue != -10000)
                    {
                        min = Math.Min(min, heightValue);
                        max = Math.Max(max, heightValue);
                    }
                }
                //output.Write(buffer, 0, metadata.Width * metadata.BitsPerSample / 8);
            }

            // compute range of heights so we can normalize the values for the grayscale bmp
            range = max - min;

            if (!string.IsNullOrEmpty(bitmapFilename))
            {
                DiagnosticUtils.OutputDebugBitmap(data, min, max, bitmapFilename, -10000);
            }

            metadata.Width /= increment;
            metadata.Height /= increment;
            metadata.PixelScaleX *= increment;
            metadata.PixelScaleY *= increment;

            return data;
        }
       

        public void ConvertToHeightMap(string inputFile, string outputBinary, string outputMetadata, string outputDiagnosticBitmap)
        {
            var metadata = ParseMetadata(inputFile);

            float[,] data = WriteBinary(inputFile, outputDiagnosticBitmap, metadata);

            using (var fileStream = File.OpenWrite(outputBinary))
            {
                ScaleBinary.Reduce(metadata, data, fileStream, 64000);
            }

            File.WriteAllText(outputMetadata, JsonConvert.SerializeObject(metadata, Formatting.Indented));
            
            _tiff.Close();
        }
    }
}
