using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Windows.Forms;

using Microsoft.Kinect;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows;
//using System.Windows.Documents;
using System.Windows.Media;

using Microsoft.Speech.AudioFormat;
using Microsoft.Speech.Recognition;
using Metrilus.Aiolos;
using Metrilus.Aiolos.Core;
using Metrilus.Aiolos.Kinect;

using System.Drawing.Imaging;

namespace voice_recognition.cs
{
    public enum ImageType
    {
        Color = 0,
        Depth = 1,
        IR = 2
    }
    public partial class Form1 : Form
    {

     //   ............ Color ,DepthFrame ,IR ..............

        /// <summary>
        /// The Kinect sensor to obtain our data from.
        /// </summary>
        private KinectSensor sensor = null;

        /// <summary>
        /// A reader for getting frame data.  This particular reader can be used to read from
        /// multiple sources (in this case, color, depth, and infrared).
        /// </summary>
        private MultiSourceFrameReader frameReader = null;

        /// <summary>
        /// The raw pixel data recieved for the depth image from the Kinect sensor.
        /// </summary>
        private ushort[] rawDepthPixelData = null;
        /// <summary>
        /// The raw pixel data recieved for the infrared image from the Kinect sensor.
        /// </summary>
        private ushort[] rawIRPixelData = null;

        /// <summary>
        /// The type of image to display in the form.
        /// </summary>
        ImageType imageType = ImageType.Color;

        public Form1()
        {
            InitializeComponent();
        }

        private void Form1_Load(object sender, EventArgs e)
        {
            // Check whether there are Kinect sensors available and select the default one.
            if (KinectSensor.GetDefault() != null)
            {
                this.sensor = KinectSensor.GetDefault();

                // Check that the connect was properly retrieved and is connected.
                if (this.sensor != null)
                {
                    if ((this.sensor.KinectCapabilities & KinectCapabilities.Vision) == KinectCapabilities.Vision)
                    {
                        // Open the sensor for use.
                        this.sensor.Open();

                        // Next open the multi-source frame reader.
                        this.frameReader = this.sensor.OpenMultiSourceFrameReader(FrameSourceTypes.Color | FrameSourceTypes.Depth | FrameSourceTypes.Infrared);

                        // Retrieve the frame descriptions for each frame source.
                        FrameDescription colorFrameDescription = this.sensor.ColorFrameSource.FrameDescription;
                        FrameDescription depthFrameDescription = this.sensor.DepthFrameSource.FrameDescription;
                        FrameDescription irFrameDescription = this.sensor.InfraredFrameSource.FrameDescription;

                        // Afterwards, setup the data using the frame descriptions.
                        // Depth and infrared have just one component per pixel (depth value or infrared value).
                        this.rawDepthPixelData = new ushort[depthFrameDescription.Width * depthFrameDescription.Height * 1];
                        this.rawIRPixelData = new ushort[irFrameDescription.Width * irFrameDescription.Height * 1];

                        // Finally, set the method for handling each multi-source frame that is captured.
                        this.frameReader.MultiSourceFrameArrived += frameReader_MultiSourceFrameArrived;
                    }
                }
            }
        }
        void frameReader_MultiSourceFrameArrived(object sender, MultiSourceFrameArrivedEventArgs e)
        {
            // Try to get the frame from its reference.
            try
            {
                MultiSourceFrame frame = e.FrameReference.AcquireFrame();

                if (frame != null)
                {
                    try
                    {
                        // Then switch between the possible types of images to show, get its frame reference, then use it
                        // with the appropriate image.
                        switch (this.imageType)
                        {
                            case ImageType.Color:
                                ColorFrameReference colorFrameReference = frame.ColorFrameReference;
                                useRGBAImage(colorFrameReference);
                                break;
                            case ImageType.Depth:
                                DepthFrameReference depthFrameReference = frame.DepthFrameReference;
                                useDepthImage(depthFrameReference);
                                break;
                            case ImageType.IR:
                                InfraredFrameReference irFrameReference = frame.InfraredFrameReference;
                                useIRImage(irFrameReference);
                                break;
                        }
                    }
                    catch (Exception)
                    {
                        // Don't worry about exceptions for this demonstration.
                    }
                }
            }
            catch (Exception)
            {
                // Don't worry about exceptions for this demonstration.
            }
        }

        /// <summary>
        /// Draws color image data from the specified frame.
        /// </summary>
        /// <param name="frameReference">The reference to the color frame that should be used.</param>
        private void useRGBAImage(ColorFrameReference frameReference)
        {
            // Actually aquire the frame here and check that it was properly aquired, and use it again since it too is disposable.
            ColorFrame frame = frameReference.AcquireFrame();

            if (frame != null)
            {
                Bitmap outputImage = null;
                System.Drawing.Imaging.BitmapData imageData = null;
                // Next get the frame's description and create an output bitmap image.
                FrameDescription description = frame.FrameDescription;
                outputImage = new Bitmap(description.Width, description.Height, PixelFormat.Format32bppArgb);

                // Next, we create the raw data pointer for the bitmap, as well as the size of the image's data.
                imageData = outputImage.LockBits(new Rectangle(0, 0, outputImage.Width, outputImage.Height),
                    ImageLockMode.WriteOnly, outputImage.PixelFormat);
                IntPtr imageDataPtr = imageData.Scan0;
                int size = imageData.Stride * outputImage.Height;

                using (frame)
                {
                    // After this, we copy the image data directly to the buffer.  Note that while this is in BGRA format, it will be flipped due
                    // to the endianness of the data.
                    if (frame.RawColorImageFormat == ColorImageFormat.Bgra)
                    {
                        frame.CopyRawFrameDataToIntPtr(imageDataPtr, (uint)size);
                    }
                    else
                    {
                        frame.CopyConvertedFrameDataToIntPtr(imageDataPtr, (uint)size, ColorImageFormat.Bgra);
                    }
                }
                // Finally, unlock the output image's raw data again and create a new bitmap for the preview picture box.
                outputImage.UnlockBits(imageData);
                this.previewPictureBox.Image = outputImage;
            }
            else
            {
                Console.WriteLine("Lost frame");
            }
        }

        /// <summary>
        /// Draws depth image data from the specified frame.
        /// </summary>
        /// <param name="frameReference">The reference to the depth frame that should be used.</param>
        private void useDepthImage(DepthFrameReference frameReference)
        {
            // Actually aquire the frame here and check that it was properly aquired, and use it again since it too is disposable.
            DepthFrame frame = frameReference.AcquireFrame();

            if (frame != null)
            {
                FrameDescription description = null;
                Bitmap outputImage = null;
                using (frame)
                {
                    // Next get the frame's description and create an output bitmap image.
                    description = frame.FrameDescription;
                    outputImage = new Bitmap(description.Width, description.Height, PixelFormat.Format32bppArgb);

                    // Next, we create the raw data pointer for the bitmap, as well as the size of the image's data.
                    System.Drawing.Imaging.BitmapData imageData = outputImage.LockBits(new Rectangle(0, 0, outputImage.Width, outputImage.Height),
                        ImageLockMode.WriteOnly, outputImage.PixelFormat);
                    IntPtr imageDataPtr = imageData.Scan0;
                    int size = imageData.Stride * outputImage.Height;

                    // After this, we copy the image data into its array.  We then go through each pixel and shift the data down for the
                    // RGB values, as their normal values are too large.
                    frame.CopyFrameDataToArray(this.rawDepthPixelData);
                    byte[] rawData = new byte[description.Width * description.Height * 4];
                    int i = 0;
                    foreach (ushort point in this.rawDepthPixelData)
                    {
                        rawData[i++] = (byte)(point >> 6);
                        rawData[i++] = (byte)(point >> 4);
                        rawData[i++] = (byte)(point >> 2);
                        rawData[i++] = 255;
                    }
                    // Next, the new raw data is copied to the bitmap's data pointer, and the image is unlocked using its data.
                    System.Runtime.InteropServices.Marshal.Copy(rawData, 0, imageDataPtr, size);
                    outputImage.UnlockBits(imageData);
                }

                // Finally, the image is set for the preview picture box.
                this.previewPictureBox.Image = outputImage;
            }
        }

        /// <summary>
        /// Draws infrared image data from the specified frame.
        /// </summary>
        /// <param name="frameReference">The reference to the infrared frame that should be used.</param>
        private void useIRImage(InfraredFrameReference frameReference)
        {
            // Actually aquire the frame here and check that it was properly aquired, and use it again since it too is disposable.
            InfraredFrame frame = frameReference.AcquireFrame();

            if (frame != null)
            {
                FrameDescription description = null;
                using (frame)
                {
                    // Next get the frame's description and create an output bitmap image.
                    description = frame.FrameDescription;
                    Bitmap outputImage = new Bitmap(description.Width, description.Height, PixelFormat.Format32bppArgb);

                    // Next, we create the raw data pointer for the bitmap, as well as the size of the image's data.
                    System.Drawing.Imaging.BitmapData imageData = outputImage.LockBits(new Rectangle(0, 0, outputImage.Width, outputImage.Height),
                        ImageLockMode.WriteOnly, outputImage.PixelFormat);
                    IntPtr imageDataPtr = imageData.Scan0;
                    int size = imageData.Stride * outputImage.Height;

                    // After this, we copy the image data into its array.  We then go through each pixel and shift the data down for the
                    // RGB values, and set each one to the same value, resulting in a grayscale image, as their normal values are too large.
                    frame.CopyFrameDataToArray(this.rawIRPixelData);
                    byte[] rawData = new byte[description.Width * description.Height * 4];
                    int i = 0;
                    foreach (ushort point in this.rawIRPixelData)
                    {
                        byte value = (byte)(128 - (point >> 8));
                        rawData[i++] = value;
                        rawData[i++] = value;
                        rawData[i++] = value;
                        rawData[i++] = 255;
                    }
                    // Next, the new raw data is copied to the bitmap's data pointer, and the image is unlocked using its data.
                    System.Runtime.InteropServices.Marshal.Copy(rawData, 0, imageDataPtr, size);
                    outputImage.UnlockBits(imageData);

                    // Finally, the image is set for the preview picture box.
                    this.previewPictureBox.Image = outputImage;
                }
            }
        }

        private void Form1_FormClosing(object sender, FormClosingEventArgs e)
        {
            if (this.frameReader != null)
            {
                this.frameReader.Dispose();
            }
            if (this.sensor != null)
            {
                this.sensor.Close();
            }
        }

       
        //private void snapshotButton_Click(object sender, EventArgs e)
        //{
        //    SaveFileDialog dialog = new SaveFileDialog();
        //    dialog.Filter = "png files (*.png)|*.png";
        //    dialog.FilterIndex = 1;
        //    if (dialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
        //    {
        //        this.previewPictureBox.Image.Save(dialog.FileName);
        //    }
        //}

        //private void depthImageButton_Click(object sender, EventArgs e)
        //{
        //    this.imageType = ImageType.Depth;
        //}

        //private void irImageButton_Click(object sender, EventArgs e)
        //{
        //    this.imageType = ImageType.IR;
        //}

        //private void colorImageButton_Click_1(object sender, EventArgs e)
        //{
        //    this.imageType = ImageType.Color;
        //}


        private void button5_Click(object sender, EventArgs e)
        {
            this.imageType = ImageType.Color;
        }

        private void button6_Click(object sender, EventArgs e)
        {
            this.imageType = ImageType.Depth;

        }

        private void button7_Click(object sender, EventArgs e)
        {
            this.imageType = ImageType.IR;
        }

        private void button8_Click(object sender, EventArgs e)
        {
            SaveFileDialog dialog = new SaveFileDialog();
            dialog.Filter = "png files (*.png)|*.png";
            dialog.FilterIndex = 1;
            if (dialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
            {
                this.previewPictureBox.Image.Save(dialog.FileName);
            }
        }

        //.............................................


        private PXCMSession session;
        public Form1(PXCMSession session)
        {
            InitializeComponent();
            this.session = session;
        }

        private void button1_Click(object sender, EventArgs e)
        {
            MainForm f1 = new MainForm(session);
            f1.Show();
        }

        private void button2_Click(object sender, EventArgs e)
        {
            MainForm2 f2 = new MainForm2(session);
            f2.Show();
        }

        private void button3_Click(object sender, EventArgs e)
        {

            // Only one sensor is supported
            this.kinectSensor = KinectSensor.GetDefault();

            if (this.kinectSensor != null)
            {
                // open the sensor
                this.kinectSensor.Open();

                // grab the audio stream
                IReadOnlyList<AudioBeam> audioBeamList = this.kinectSensor.AudioSource.AudioBeams;
                System.IO.Stream audioStream = audioBeamList[0].OpenInputStream();

                // create the convert stream
                this.convertStream = new KinectAudioStream(audioStream);
            }
            else 
            {
                // on failure, set the status text
                //this.statusBarText.Text = Properties.Resources.NoKinectReady;
                return;
            }

            RecognizerInfo ri = TryGetKinectRecognizer();

            if (null != ri)
            {


                this.speechEngine = new SpeechRecognitionEngine(ri.Id);

                var directions = new Choices();
                directions.Add(new SemanticResultValue("one", "ONE"));
                directions.Add(new SemanticResultValue("two", "TWO"));
                directions.Add(new SemanticResultValue("three", "THREE"));
                directions.Add(new SemanticResultValue("four", "FOUR"));
                directions.Add(new SemanticResultValue("five", "FIVE"));
                directions.Add(new SemanticResultValue("hello", "HELLO"));
                directions.Add(new SemanticResultValue("where are you going", "WHERE ARE YOU GOING"));

                directions.Add(new SemanticResultValue("left", "LEFT"));
                directions.Add(new SemanticResultValue("right", "RIGHT"));

                directions.Add(new SemanticResultValue(" good bye", "GOOD BYE"));
                directions.Add(new SemanticResultValue("good morning", "GOOD MORNING"));

                directions.Add(new SemanticResultValue("where is the toilet ", "WHERE IS THE TOILET"));
                directions.Add(new SemanticResultValue("where do you live ", "WHERE DO YOU LIVE"));

                //left right  good bye good morning where is the toilet where do you live
                // LEFT RIGHT GOOD BYE GOOD MORNING WHERE IS THE TOILET WHERE DO YOU LIVE
                
                var gb = new GrammarBuilder { Culture = ri.Culture };
                gb.Append(directions);

                var g = new Grammar(gb);


                this.speechEngine.LoadGrammar(g);


                this.speechEngine.SpeechRecognized += this.SpeechRecognized;
                this.speechEngine.SpeechRecognitionRejected += this.SpeechRejected;

                // let the convertStream know speech is going active
                this.convertStream.SpeechActive = true;

                // For long recognition sessions (a few hours or more), it may be beneficial to turn off adaptation of the acoustic model. 
                // This will prevent recognition accuracy from degrading over time.
                ////speechEngine.UpdateRecognizerSetting("AdaptationOn", 0);

                this.speechEngine.SetInputToAudioStream(
                    this.convertStream, new SpeechAudioFormatInfo(EncodingFormat.Pcm, 16000, 16, 1, 32000, 2, null));
                this.speechEngine.RecognizeAsync(RecognizeMode.Multiple);
            }
            else
            {
                //this.statusBarText.Text = Properties.Resources.NoSpeechRecognizer;
            }
        }

        private const string MediumGreyBrushKey = "MediumGreyBrush";


        /// <summary>
        /// Active Kinect sensor.
        /// </summary>
        private KinectSensor kinectSensor = null;

        /// <summary>
        /// Stream for 32b-16b conversion.
        /// </summary>
        private KinectAudioStream convertStream = null;

        /// <summary>
        /// Speech recognition engine using audio data from Kinect.
        /// </summary>
        private SpeechRecognitionEngine speechEngine = null;


        /// <summary>
        /// List of all UI span elements used to select recognized text.
        /// </summary>
        private List<TimeSpan> recognitionSpans;

        /// <summary>
        /// Initializes a new instance of the MainWindow class.
        /// </summary>

        private static RecognizerInfo TryGetKinectRecognizer()
        {
            IEnumerable<RecognizerInfo> recognizers;

            // This is required to catch the case when an expected recognizer is not installed.
            // By default - the x86 Speech Runtime is always expected. 
            try
            {
                recognizers = SpeechRecognitionEngine.InstalledRecognizers();
            }
            catch (COMException)
            {
                return null;
            }

            foreach (RecognizerInfo recognizer in recognizers)
            {
                string value;
                recognizer.AdditionalInfo.TryGetValue("Kinect", out value);
                if ("True".Equals(value, StringComparison.OrdinalIgnoreCase) &&
                    "en-US".Equals(recognizer.Culture.Name, StringComparison.OrdinalIgnoreCase))
                {
                    return recognizer;
                }
            }

            return null;
        }

        /// <summary>
        /// Execute initialization tasks.
        /// </summary>
        /// <param name="sender">object sending the event</param>
        /// <param name="e">event arguments</param>

        private void WindowClosing(object sender, CancelEventArgs e)
        {
            if (null != this.convertStream)
            {
                this.convertStream.SpeechActive = false;
            }

            if (null != this.speechEngine)
            {
                this.speechEngine.SpeechRecognized -= this.SpeechRecognized;
                this.speechEngine.SpeechRecognitionRejected -= this.SpeechRejected;
                this.speechEngine.RecognizeAsyncStop();
            }

            if (null != this.kinectSensor)
            {
                this.kinectSensor.Close();
                this.kinectSensor = null;
            }
        }

        /// <summary>
        /// Remove any highlighting from recognition instructions.
        /// </summary>
        private void ClearRecognitionHighlights()
        {

        }

        /// <summary>
        /// Handler for recognized speech events.
        /// </summary>
        /// <param name="sender">object sending the event.</param>
        /// <param name="e">event arguments.</param>
        private void SpeechRecognized(object sender, SpeechRecognizedEventArgs e)
        {
            // Speech utterance confidence below which we treat speech as if it hadn't been heard
            const double ConfidenceThreshold = 0.3;

            // Number of degrees in a right angle.
            const int DegreesInRightAngle = 90;

            // Number of pixels turtle should move forwards or backwards each time.
            const int DisplacementAmount = 60;

            this.ClearRecognitionHighlights();

            if (e.Result.Confidence >= ConfidenceThreshold)
            {
                switch (e.Result.Semantics.Value.ToString())
                {

                    // axWindowsMediaPlayer1.URL =(@"E:\Kinect\01 aiolos\mine\SignLang-ASLanim\one.avi");
                    case "ONE":
                        axWindowsMediaPlayer1.URL = (@"E:\Kinect\01 aiolos\mine\SignLang-ASLanim\one.avi");
                        break;
                    case "TWO":
                        axWindowsMediaPlayer1.URL = (@"E:\Kinect\01 aiolos\mine\SignLang-ASLanim\two.avi");
                        break;
                    case "THREE":
                        axWindowsMediaPlayer1.URL = (@"E:\Kinect\01 aiolos\mine\SignLang-ASLanim\three.avi");
                        break;
                    case "FOUR":
                        axWindowsMediaPlayer1.URL = (@"E:\Kinect\01 aiolos\mine\SignLang-ASLanim\four.avi");
                        break;
                    case "FIVE":
                        axWindowsMediaPlayer1.URL = (@"E:\Kinect\01 aiolos\mine\SignLang-ASLanim\five.avi");
                        break;
                    case "HELLO":
                        axWindowsMediaPlayer1.URL = (@"E:\Kinect\01 aiolos\mine\SignLang-ASLanim\hello.avi");
                        break;
                    case "WHERE ARE YOU GOING":
                        axWindowsMediaPlayer1.URL = (@"E:\Kinect\01 aiolos\mine\SignLang-ASLanim\where are you going.avi");
                        break;

                    case "WHERE DO YOU LIVE":
                        axWindowsMediaPlayer1.URL = (@"E:\Kinect\01 aiolos\mine\SignLang-ASLanim\where do you live.avi");
                        break;
                    case "WHERE IS THE TOILET":
                        axWindowsMediaPlayer1.URL = (@"E:\Kinect\01 aiolos\mine\SignLang-ASLanim\where is the toilet.avi");
                        break;
                    case "GOOD BYE":
                        axWindowsMediaPlayer1.URL = (@"E:\Kinect\01 aiolos\mine\SignLang-ASLanim\good bye.avi");
                        break;
                    case "GOOD MORNING":
                        axWindowsMediaPlayer1.URL = (@"E:\Kinect\01 aiolos\mine\SignLang-ASLanim\good morning.avi");
                        break;

                    case "RIGHT":
                        axWindowsMediaPlayer1.URL = (@"E:\Kinect\01 aiolos\mine\SignLang-ASLanim\right.avi");
                        break;

                    case "LEFT":
                        axWindowsMediaPlayer1.URL = (@"C:\Users\Admin\Documents\tesis\SL Video\SignLang-ASLanim\left.avi");
                        break;

                    // LEFT RIGHT GOOD BYE GOOD MORNING WHERE IS THE TOILET WHERE DO YOU LIVE
                
                    
                }
            }
        }

        /// <summary>
        /// Handler for rejected speech events.
        /// </summary>
        /// <param name="sender">object sending the event.</param>
        /// <param name="e">event arguments.</param>
        private void SpeechRejected(object sender, SpeechRecognitionRejectedEventArgs e)
        {
            this.ClearRecognitionHighlights();
        }

        private void button4_Click(object sender, EventArgs e)
        {
            this.Close();
        }

        private void previewPictureBox_Click(object sender, EventArgs e)
        {

        }

        private void axWindowsMediaPlayer1_Enter(object sender, EventArgs e)
        {

        }

        private void groupBox1_Enter(object sender, EventArgs e)
        {

        }

        //private void button9_Click(object sender, EventArgs e)
        //{
           // speechEngine.RecognizeAsyncStop();
            //button3.Enabled = true;
            //button9.Enabled = false;
          
       // }

        

        
         /// <summary>
         /// ///////////////////////////////////  COLOR DEPTH IR
         /// </summary>
         /// <param name="sender"></param>
         /// <param name="e"></param>
      


    }
}
