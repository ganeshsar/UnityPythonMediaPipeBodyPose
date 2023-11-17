# Internally used, don't mind this.
KILL_THREADS = False

# Toggle this in order to view how your WebCam is being interpreted (reduces performance).
DEBUG = True 

# To switch cameras. Sometimes takes a while.
WEBCAM_INDEX = 0

# Settings do not universally apply, not all WebCams support all frame rates and resolutions
USE_CUSTOM_CAM_SETTINGS = False
FPS = 60
WIDTH = 320
HEIGHT = 240

# [0, 2] Higher numbers are more precise, but also cost more performance. Good environment conditions = 1, otherwise 2.
MODEL_COMPLEXITY = 1