# Multithreaded Unity Python MediaPipe Body/Pose

NOTICE: this project has been replaced by [Tracking4All](https://ko-fi.com/s/709e6d6f6f) which is actively supported and features much better quality, is cross-platform with embedded Unity support and more!

## Overview
This is a project that tests Google MediaPipePose inside of Unity using Python bindings. Reading the WebCam and running the model occur on different threads. Basically, it's configurable full body tracking using a WebCam. What's more is that you can stay inside of a Python environment for all the mediapipe stuff (use all your usual libraries).<br>
![image showing waving](https://ganthefan.com/images/bodygif.gif)


## Installation
1. Install Python and Unity (2021.3.17f1 was used, but any version close to that should be fine).
2. pip install mediapipe
3. Clone/download this repository.
4. Run main.py using Python.
5. Run the Unity project.
6. Go back to the Unity view to see your body being tracked in real time.

### Notes:
* See global_vars.py for some basic configuration options to speed up/improve precision of the detection.
* Wearing clothing that contrasts with a background helps a fair bit.
* The architecture and performance has been greatly improved since my last project where I experimented with MediaPipeHands.

[![image showing new version](https://ganthefan.com/images/poseReplacement.png)](https://ko-fi.com/s/709e6d6f6f)
