# WpfImageViewer

A simple fullscreen Wpf-based image viewer that closes when you press Esc.

## Notes on current version

### Keyboard commands:
* Esc: close application
* Home: first file in fileList
* End: last file in fileList
* Left Arrow: previous file in fileList
* Right Arrow: next file in fileList
* Spacebar: center image & reset zoom level

### Mouse
* Left Mouse Button: drag the image around the screen
    * double-click: open an image
* Right Mouse Button: copy path of current image directory
    * double-click: copy full path of current image
* Middle Mouse Button: open Explorer with current file selected
* Mousewheel: Zoom in/out

### Config
Some parameters can be overridden in WpfImageViewer.exe.config, conveniently 
located somewhere like
`%LOCALAPPDATA%\Apps\2.0\{random}\{random}\wpfi..{random}\`
for ClickOnce applications.
* ApplicationTitle
    * set the name of the application, e.g. shown during Alt-Tab
    * default: Wpf Image Viewer
* FadeoutSeconds
    * decimal amount of seconds before status text disappears
    * default: 4 seconds
    * 0 disables status text
    * negative values disable fadeout
* IncludedFileFormats
    * extensions to include when trying to view files
    * include dots before extension, separate by only comma, no space
    * default: .bmp,.gif,.jpeg,.jpg,.png,.tif,.tiff
* BackgroundColor
    * tries to set background using a color name, e.g. Pink
    * default: Black
* ZoomMin
    * minimum zoom amount
    * default: 0.5
* ZoomMax
    * maximum zoom amount
    * default: 5
* ZoomStep
    * change in zoom amount per step
    * default: 1.25
