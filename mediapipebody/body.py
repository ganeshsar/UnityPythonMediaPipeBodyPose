# MediaPipe Body
import mediapipe as mp
from mediapipe.tasks import python
from mediapipe.tasks.python import vision
import numpy as np

import cv2
import threading
import time
import global_vars 
import struct

# the capture thread captures images from the WebCam on a separate thread (for performance)
class CaptureThread(threading.Thread):
    cap = None
    ret = None
    frame = None
    isRunning = False
    counter = 0
    timer = 0.0
    def run(self):
        self.cap = cv2.VideoCapture(global_vars.WEBCAM_INDEX) # sometimes it can take a while for certain video captures 4
        if global_vars.USE_CUSTOM_CAM_SETTINGS:
            self.cap.set(cv2.CAP_PROP_FPS, global_vars.FPS)
            self.cap.set(cv2.CAP_PROP_FRAME_WIDTH,global_vars.WIDTH)
            self.cap.set(cv2.CAP_PROP_FRAME_HEIGHT,global_vars.HEIGHT)

        time.sleep(1)
        
        print("Opened Capture @ %s fps"%str(self.cap.get(cv2.CAP_PROP_FPS)))
        while not global_vars.KILL_THREADS:
            self.ret, self.frame = self.cap.read()
            self.isRunning = True
            if global_vars.DEBUG:
                self.counter = self.counter+1
                if time.time()-self.timer>=3:
                    print("Capture FPS: ",self.counter/(time.time()-self.timer))
                    self.counter = 0
                    self.timer = time.time()

# the body thread actually does the 
# processing of the captured images, and communication with unity
class BodyThread(threading.Thread):
    data = ""
    dirty = True
    pipe = None
    timeSinceCheckedConnection = 0
    timeSincePostStatistics = 0

    def compute_real_world_landmarks(self,world_landmarks,image_landmarks,image_shape):
        try:
            # pseudo camera internals
            # if you properly calibrated your camera tracking quality can improve...
            frame_height,frame_width, channels = image_shape
            focal_length = frame_width*.6
            center = (frame_width/2, frame_height/2)
            camera_matrix = np.array(
                                    [[focal_length, 0, center[0]],
                                    [0, focal_length, center[1]],
                                    [0, 0, 1]], dtype = "double"
                                    )
            distortion = np.zeros((4, 1))

            success, rotation_vector, translation_vector = cv2.solvePnP(objectPoints= world_landmarks, 
                                                                        imagePoints= image_landmarks, 
                                                                        cameraMatrix= camera_matrix, 
                                                                        distCoeffs= distortion,
                                                                        flags=cv2.SOLVEPNP_SQPNP)
            transformation = np.eye(4)
            transformation[0:3, 3] = translation_vector.squeeze()

            # transform model coordinates into homogeneous coordinates
            model_points_hom = np.concatenate((world_landmarks, np.ones((33, 1))), axis=1)

            # apply the transformation
            world_points = model_points_hom.dot(np.linalg.inv(transformation).T)

            return world_points
        except AttributeError:
            print("Attribute Error: shouldn't happen frequently")
            return world_landmarks 

    def run(self):
        mp_drawing = mp.solutions.drawing_utils
        mp_pose = mp.solutions.pose
        
        capture = CaptureThread()
        capture.start()

        with mp_pose.Pose(min_detection_confidence=0.80, min_tracking_confidence=0.5, model_complexity = global_vars.MODEL_COMPLEXITY,static_image_mode = False,enable_segmentation = True) as pose: 
            
            while not global_vars.KILL_THREADS and capture.isRunning==False:
                print("Waiting for camera and capture thread.")
                time.sleep(0.5)
            print("Beginning capture")
                
            while not global_vars.KILL_THREADS and capture.cap.isOpened():
                ti = time.time()

                # Fetch stuff from the capture thread
                ret = capture.ret
                image = capture.frame
                                
                # Image transformations and stuff
                #image = cv2.flip(image, 1)
                image.flags.writeable = global_vars.DEBUG
                
                # Detections
                results = pose.process(image)
                tf = time.time()
                
                # Rendering results
                if global_vars.DEBUG:
                    if time.time()-self.timeSincePostStatistics>=1:
                        print("Theoretical Maximum FPS: %f"%(1/(tf-ti)))
                        self.timeSincePostStatistics = time.time()
                        
                    if results.pose_landmarks:
                        mp_drawing.draw_landmarks(image, results.pose_landmarks, mp_pose.POSE_CONNECTIONS, 
                                                mp_drawing.DrawingSpec(color=(255, 100, 0), thickness=2, circle_radius=4),
                                                mp_drawing.DrawingSpec(color=(255, 255, 255), thickness=2, circle_radius=2),
                                                )
                    cv2.imshow('Body Tracking', image)
                    cv2.waitKey(1)

                if self.pipe==None and time.time()-self.timeSinceCheckedConnection>=1:
                    try:
                        self.pipe = open(r'\\.\pipe\UnityMediaPipeBody', 'r+b', 0)
                    except FileNotFoundError:
                        print("Waiting for Unity project to run...")
                        self.pipe = None
                    self.timeSinceCheckedConnection = time.time()
                    
                if self.pipe != None:
                    # Set up data for piping
                    self.data = ""
                    i = 0
                    
                    if results.pose_world_landmarks:
                        image_landmarks = results.pose_landmarks
                        world_landmarks = results.pose_world_landmarks

                        model_points = np.float32([[-l.x, -l.y, -l.z] for l in world_landmarks.landmark])
                        image_points = np.float32([[l.x * image.shape[1], l.y * image.shape[0]] for l in image_landmarks.landmark])
                        
                        body_world_landmarks_world = self.compute_real_world_landmarks(model_points,image_points,image.shape)
                        body_world_landmarks = results.pose_world_landmarks
                        
                        for i in range(0,33):
                            self.data += "FREE|{}|{}|{}|{}\n".format(i,body_world_landmarks_world[i][0],body_world_landmarks_world[i][1],body_world_landmarks_world[i][2])
                        for i in range(0,33):
                            self.data += "ANCHORED|{}|{}|{}|{}\n".format(i,-body_world_landmarks.landmark[i].x,-body_world_landmarks.landmark[i].y,-body_world_landmarks.landmark[i].z)

                    
                    s = self.data.encode('utf-8') 
                    try:     
                        self.pipe.write(struct.pack('I', len(s)) + s)   
                        self.pipe.seek(0)    
                    except Exception as ex:  
                        print("Failed to write to pipe. Is the unity project open?")
                        self.pipe= None
                        
                #time.sleep(1/20)
                        
        self.pipe.close()
        capture.cap.release()
        cv2.destroyAllWindows()