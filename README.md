# QtmVideoGridRender
Render grid videos from a bunch of video files and include timestamp in the new mpeg4 video (smpte if available)

1. It will go through all subfolders of the starting folder.
2. If a .qtm file is found it will locate all associated Oqus/Miqus/Arqus Video .avi files
3. It will extract smpte time code from the avi file(s) if available.
4. It will design a video grid depending on number of avi files, 4 = 2x2 grid.
5. Video Encoding is using ffmpeg in mjpeg, using highest bitrate of all the videos.
6. If an already existing filename.qtm/.avi exist then it will not recreate it.

