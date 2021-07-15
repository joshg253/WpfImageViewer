using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;

namespace WpfImageViewer
{
  /// <summary>
  /// Interaction logic for MainWindow.xaml
  /// </summary>
  public partial class MainWindow : Window
  {
    // import settings from config file
    string applicationTitle = Properties.Settings.Default.ApplicationTitle;
    string backgroundColor = Properties.Settings.Default.BackgroundColor;
    string[] fileFormats; //need to split the string before setting it
    double fadeoutSeconds = Properties.Settings.Default.FadeoutSeconds;
    double zoomMin = Properties.Settings.Default.ZoomMin;
    double zoomMax = Properties.Settings.Default.ZoomMax;
    double zoomStep = Properties.Settings.Default.ZoomStep;

    //related to file navigation
    IEnumerable<string> fileList = new List<string>();  //list of files in dir of opened image
    int currentFileIndex = new int(); //file position # in list of files
    string imagePath;

    Point center; //center of screen
    Matrix matrix = Matrix.Identity;  // used for zoom function

    //related to image dragging
    Point mousePosition;    //saved mouse position
    bool captured = false;  //whether or not the mouse is currently captured
    Point imagePosition;    //saved image position
    Point currentPosition;  //pointer position for comparison to saved positions

    DispatcherTimer timer = new DispatcherTimer();  //used for status text fadeout

    public MainWindow()
    {
      InitializeComponent();

      //application title from config file
      try
      {
        Window1.Title = applicationTitle;
      }
      catch
      {
        Window1.Title = "WpfImageViewer";
      }

      //application background color from config file
      SolidColorBrush backgroundFill;
      try
      {
        backgroundFill = (SolidColorBrush)new BrushConverter().ConvertFromString(backgroundColor);
      }
      catch
      {
        backgroundFill = (SolidColorBrush)new BrushConverter().ConvertFromString("Black");
      }
      Window1.Background = backgroundFill;
      Grid1.Background = backgroundFill;
      Canvas1.Background = backgroundFill;

      //file formats from config file
      try
      {
        string includedFileFormats = Properties.Settings.Default.IncludedFileFormats;
        fileFormats = includedFileFormats.Split(',');
      }
      catch
      {
        fileFormats = new[] { ".jpg", ".png", ".bmp", ".jpeg", ".gif", ".tif", ".tiff" };
      }

      PreviewKeyDown += new KeyEventHandler(HandleKey);   //handle keypresses
    }

    /// <summary>
    /// wait until Window1 is Loaded() so we can get its size before displaying an image
    /// </summary>
    /// <param name="sender"></param>
    /// <param name="e"></param>
    private void Window1_Loaded(object sender, RoutedEventArgs e)
    {
      //calculate center of screen
      center = new Point(Grid1.ActualWidth / 2, Grid1.ActualHeight / 2);
      //Debug.WriteLine("DEBUG: center.X,center.Y " + center.X + "," + center.Y);

      //handle command-line args
      string[] args = Environment.GetCommandLineArgs(); //array of command-line arguments

      ////DEBUG: list the cmd-line args
      //Debug.WriteLine("DEBUG: parameter count = {0}", args.Length);
      //for (int i = 0; i < args.Length; i++)
      //{
      //  Debug.WriteLine("DEBUG: Arg[{0}] = [{1}]", i, args[i]);
      //}

      if (args.Length > 1)  // 1 arg means just the exe
      {
        imagePath = Path.GetFullPath(args[1]);

        //Debug.WriteLine("DEBUG: calling SetImage(" + imagePath + ")");
        SetImage();
      }
    }

    /// <summary>
    /// sets the image, updates the file list/index
    /// </summary>
    /// <param name="imagePath"></param>
    private void SetImage()
    {
      try
      {
        Uri imageUri = new Uri(imagePath);
        string directoryName;

        //if imagePath is a file,
        if (File.Exists(imagePath))
        {
          //Debug.WriteLine("DEBUG: imagePath is a file " + imagePath);

          BitmapImage imageBitmap = new BitmapImage(imageUri);

          //WxH of actual image file
          //Debug.WriteLine("DEBUG: imageBitmap WxH " + Math.Round(imageBitmap.Width) + "x" + Math.Round(imageBitmap.Height));

          //WxH of application window -- does not match screen resoution
          //Debug.WriteLine("DEBUG: Window1 WxH " + Window1.ActualWidth + "x" + Window1.ActualHeight);

          //WxH of Grid1 -- matches screen resoution
          //Debug.WriteLine("DEBUG: Grid1 WxH " + Grid1.ActualWidth + "x" + Grid1.ActualHeight);

          //determine Stretch based upon image size vs screen size
          if (imageBitmap.Width <= Grid1.ActualWidth && imageBitmap.Height <= Grid1.ActualHeight)
          {
            //Debug.WriteLine("DEBUG: image same or smaller than screen.");
            Image1.Stretch = Stretch.None;
          }
          else
          {
            //Debug.WriteLine("DEBUG: image larger than screen.");
            Image1.Stretch = Stretch.Uniform;
          }
          //Debug.WriteLine("DEBUG: Image1.Stretch " + Image1.Stretch);

          Image1.Source = imageBitmap;
          Canvas.SetLeft(Image1, 0);
          Canvas.SetTop(Image1, 0);
          Image1.Visibility = Visibility.Visible;

          Label1.Content = imagePath; //set the status text as the full filepath

          directoryName = Path.GetDirectoryName(imagePath); //get the parent directory of the image
          UpdateFileList(directoryName);  //parse the files in the parent directory

          ////DEBUG: list the files in fileList
          //Debug.WriteLine("DEBUG: fileList " + fileList);
          //foreach (string f in fileList)
          //{
          //  Debug.WriteLine("DEBUG: fileList f " + f);
          //}

          currentFileIndex = fileList.ToList().IndexOf(imagePath);  //update current file index
          //Debug.WriteLine("DEBUG: currentFileIndex " + currentFileIndex);
        }

        //if imagePath is a directory,
        else if (Directory.Exists(imagePath))
        {
          //Debug.WriteLine("DEBUG: imagePath is a directory " + imagePath);

          directoryName = imagePath;  //imagePath is already a directory
          UpdateFileList(directoryName);
          ////DEBUG: list the files in fileList
          //Debug.WriteLine("DEBUG: fileList " + fileList);
          //foreach (string f in fileList)
          //{
          //  Debug.WriteLine("DEBUG: fileList f " + f);
          //}
          imagePath = fileList.First();
          SetImage(); //try to load the first file in the directory
        }

        //imagePath is neither a file nor a directory,
        else
        {
          //Debug.WriteLine("DEBUG: Invalid path " + imagePath);
          Label1.Content = "Invalid path: " + imagePath;
          Image1.Source = null; //show blank screen, maybe placeholder image at some point
        }
      }
      catch
      {
        //Debug.WriteLine("DEBUG: Invalid image " + imagePath);
        Label1.Content = "Invalid image: " + imagePath;

        //still need to update fileList & index
        var directoryName = Path.GetDirectoryName(imagePath); //get the parent directory of the image
        UpdateFileList(directoryName);  //parse the files in the parent directory
        currentFileIndex = fileList.ToList().IndexOf(imagePath);
        //Debug.WriteLine("DEBUG: currentFileIndex " + currentFileIndex);

        //Image1.Source = null;   //show blank screen, maybe placeholder image at some point
        Image1.Visibility = Visibility.Hidden;  //source file is used for middle-click, so can't set to null
      }

      // set the text visible and start the countdown to make it disappear
      StartFadeTimer();
    }

    /// <summary>
    /// updates the fileList with only files whose extensions are in imageFileExtensions
    /// </summary>
    /// <param name="directoryName"></param>
    private void UpdateFileList(string directoryName)
    {
      if (fileFormats[0] == "*")
      {
        fileList = Directory
              .EnumerateFiles(directoryName);
      }
      else
      {
        fileList = Directory
            .EnumerateFiles(directoryName)
            .Where(f => fileFormats.Any(f.ToLower().EndsWith));
      }

      //attempt to sort fileList lexicographically
      // Use LINQ to sort the array received and return a copy.
      fileList = from filename in fileList
                 orderby Regex.Replace(filename, @"\d+", n => n.Value.PadLeft(4, '0'))
                 //orderby filename.Length ascending, filename
                 select filename;
    }

    /// <summary>
    /// handles keypresses
    /// </summary>
    /// <param name="sender"></param>
    /// <param name="e"></param>
    private void HandleKey(object sender, KeyEventArgs e)
    {
      //Debug.WriteLine("DEBUG: " + e.Key + " pressed");

      if (Keyboard.Modifiers == ModifierKeys.None)
      {
        switch (e.Key)
        {
          //Esc Key: close application
          case Key.Escape:
            Close();
            break;

          //Right Arrow: try to load next file in fileList
          case Key.Right:
            //if not last file...
            if (currentFileIndex < fileList.Count() - 1)
            {
              //...try to load next file
              imagePath = fileList.ElementAt(currentFileIndex + 1);
              SetImage();
            }
            break;

          //Left Arrow: try to load previous file in fileList
          case Key.Left:
            //if not the first file...
            if (currentFileIndex > 0)
            {
              //...try to load previous file
              imagePath = fileList.ElementAt(currentFileIndex - 1);
              SetImage();
            }
            break;

          //Home Key: load first file in fileList
          case Key.Home:
            //if not the first file...
            if (currentFileIndex > 0)
            {
              //...try to load the first file in the filelist
              imagePath = fileList.First();
              SetImage();
            }
            break;

          //End Key: load last file in fileList
          case Key.End:
            //if not last file...
            if (currentFileIndex < fileList.Count() - 1)
            {
              //...try to load the last file in fileList
              imagePath = fileList.ElementAt(fileList.Count() - 1);
              SetImage();
            }
            break;

          //Z Key: cycle through Stretch types
          //case Key.Z:
          //  //  Fill (don't keep aspect), None, Uniform (default, keep aspect), UniformToFill (keep aspect, clip)
          //  if (Image1.Stretch == Stretch.Fill)
          //    Image1.Stretch = Stretch.None;
          //  else if (Image1.Stretch == Stretch.None)
          //    Image1.Stretch = Stretch.Uniform;
          //  else if (Image1.Stretch == Stretch.Uniform)
          //    Image1.Stretch = Stretch.UniformToFill;
          //  else if (Image1.Stretch == Stretch.UniformToFill)
          //    Image1.Stretch = Stretch.Fill;
          //  Label1.Content = "Stretch Mode: " + Image1.Stretch;
          //  StartFadeTimer();
          //  break;

          //Spacebar: reset zoom and center image
          case Key.Space:
            matrix.SetIdentity();
            Image1.RenderTransform = new MatrixTransform(matrix);
            Canvas.SetLeft(Image1, 0);
            Canvas.SetTop(Image1, 0);
            break;
          
          //Plus keys: zoom in
          case Key.Add:
          case Key.OemPlus:
            Zoom(120, Image1.PointFromScreen(center));
            break;

          //Minus keys: zoom out
          case Key.Subtract:
          case Key.OemMinus:
            Zoom(-120, Image1.PointFromScreen(center));
            break;

          //Other key: do nothing
          default:
            break;
        }
      }
    }

    /// <summary>
    /// handles mouse buttons on the Grid
    /// </summary>
    /// <param name="sender"></param>
    /// <param name="e"></param>
    private void Grid1_MouseDown(object sender, MouseButtonEventArgs e)
    {
      //Debug.WriteLine("DEBUG: enter Grid1_MouseDown");

      if (e.ChangedButton == MouseButton.Left && e.ClickCount == 2)
      {
        //Debug.WriteLine("DEBUG: double-clicked left mouse button");

        OpenFileDialog openFileDialog = new OpenFileDialog();

        if (openFileDialog.ShowDialog() == true)
        {
          imagePath = openFileDialog.FileName;
          SetImage();
        }
      }

      //string imagePath = "";
      //string imageDir = "";

      if (Image1.Source != null)
      {
        //  imagePath = Image1.Source.ToString();
        var imageDir = Path.GetDirectoryName(imagePath);
        //}
        //Debug.WriteLine("DEBUG: imagePath " + imagePath);
        //Debug.WriteLine("DEBUG: imageDir " + imageDir);

        if (e.ChangedButton == MouseButton.Middle)
        {
          //Debug.WriteLine("DEBUG: middle mouse button clicked");
          //Debug.WriteLine("DEBUG: imagePath " + imagePath);
          Process.Start("explorer.exe", "/select, " + imagePath);
        }

        if (e.ChangedButton == MouseButton.Right)
        {
          //Debug.WriteLine("DEBUG: right mouse button clicked");
          if (e.ClickCount == 2)   //double-click right mouse button
          {
            //copy full image path to clipboard
            Clipboard.SetText(imagePath);

            Label1.Content = "Image path copied to clipboard";
            StartFadeTimer();
          }
          else  //single-click right mouse button
          {
            //copy full path to clipboard
            Clipboard.SetText(imageDir);

            Label1.Content = "Directory path copied to clipboard";
            StartFadeTimer();
          }
        }
      }
    }

    /// <summary>
    /// handles mousewheel scrolling
    /// </summary>
    /// <param name="sender"></param>
    /// <param name="e"></param>
    private void Grid1_HandleMouseWheel(object sender, MouseWheelEventArgs e)
    {
      //Debug.WriteLine("DEBUG: enter Grid1_HandleMouseWheel");

      //Debug.WriteLine("DEBUG: sender " + sender);
      //Debug.WriteLine("DEBUG: e " + e);
      //Debug.WriteLine("DEBUG: e.Delta " + e.Delta);

      if (Image1.Source != null)
      {
        Point position = e.GetPosition(Image1);
        //Debug.WriteLine("DEBUG: e.GetPosition(element) " + position);

        Zoom(e.Delta, position);
      }
    }

    /// <summary>
    /// handles clicks on an image
    /// </summary>
    /// <param name="sender"></param>
    /// <param name="e"></param>
    private void Image1_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
      //Debug.WriteLine("DEBUG: enter Image1_MouseLeftButtonDown");

      //if there is an image and mouse is successfully captured
      if (Image1.Source != null && Image1.CaptureMouse())
      {
        captured = true;

        //save current mouse position
        mousePosition = e.GetPosition(Canvas1);

        //save current image position
        imagePosition.X = Canvas.GetLeft(Image1);
        imagePosition.Y = Canvas.GetTop(Image1);
      }
    }

    /// <summary>
    /// 
    /// </summary>
    /// <param name="sender"></param>
    /// <param name="e"></param>
    private void Image1_MouseMove(object sender, MouseEventArgs e)
    {
      //Debug.WriteLine("DEBUG: enter Image1_MouseMove");

      if (captured)
      {
        //Debug.WriteLine("DEBUG: mouse is captured.");

        //get current mouse position
        currentPosition = e.GetPosition(Canvas1);

        //update image position based upon mouse movement
        imagePosition += currentPosition - mousePosition;
        Canvas.SetLeft(Image1, imagePosition.X);
        Canvas.SetTop(Image1, imagePosition.Y);

        //update saved mouse position
        mousePosition = currentPosition;
      }
    }

    /// <summary>
    /// releases the mouse cursor
    /// </summary>
    /// <param name="sender"></param>
    /// <param name="e"></param>
    private void Image1_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
      //Debug.WriteLine("DEBUG: enter Image1_MouseLeftButtonUp");

      //release mouse
      Mouse.Capture(null);
      captured = false;
    }

    /// <summary>
    /// uses a matrix transform to zoom
    /// </summary>
    /// <param name="eDelta"></param>
    /// <param name="position"></param>
    private void Zoom(double eDelta, Point position)
    {
      matrix = Image1.RenderTransform.Value;
      //Debug.WriteLine("DEBUG: matrix.M11 " + matrix.M11);
      //Debug.WriteLine("DEBUG: matrix.M12 " + matrix.M12);
      //Debug.WriteLine("DEBUG: matrix.M21 " + matrix.M21);
      //Debug.WriteLine("DEBUG: matrix.M22 " + matrix.M22);
      //Debug.WriteLine("DEBUG: matrix.OffsetX " + matrix.OffsetX);
      //Debug.WriteLine("DEBUG: matrix.OffsetY " + matrix.OffsetY);

      var scale = eDelta > 0 ? zoomStep : (1.0 / zoomStep); //determine which way to zoom
      matrix.ScaleAtPrepend(scale, scale, position.X, position.Y);

      if (matrix.M11 > zoomMin && matrix.M11 < zoomMax) //restrict zoom in and out amounts
      {
        Image1.RenderTransform = new MatrixTransform(matrix);
      }
    }

    /// <summary>
    /// starts a countdown to hide the status text after some time
    /// </summary>
    private void StartFadeTimer()
    {
      double fadeoutSeconds = Properties.Settings.Default.FadeoutSeconds;
      // zero value in config file disables status text
      if (fadeoutSeconds == 0)
        return;

      Label1.Visibility = Visibility.Visible;

      // negative value in config file disables fadeout
      if (fadeoutSeconds < 0)
        return;

      else
      {
        timer.Interval = TimeSpan.FromSeconds(fadeoutSeconds);
        timer.Tick += TimerTick;
        timer.Start();
      }
    }

    /// <summary>
    /// hides the text and stops the timer
    /// </summary>
    /// <param name="sender"></param>
    /// <param name="e"></param>
    private void TimerTick(object sender, EventArgs e)
    {
      //DispatcherTimer timer = (DispatcherTimer)sender;
      timer.Stop();
      timer.Tick -= TimerTick;
      Label1.Visibility = Visibility.Hidden;
    }

  }
}
