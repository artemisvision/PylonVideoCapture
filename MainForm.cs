using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Drawing.Imaging;
using System.Text;
using System.Windows.Forms;
using PylonLiveView;
using PylonC.NETSupportLibrary;
using PylonC.NET;

namespace PylonLiveView
{
    /* The main window. */
    public partial class MainForm : Form
    {

        AviWriter aw;
        private ImageProvider m_imageProvider = new ImageProvider(); /* Create one image provider. */
        private Bitmap m_bitmap = null; /* The bitmap is used for displaying the image. */
        private bool is_recording = false;
        private float frame_rate = 12;
        private Multimedia.Timer my_trigger;
        private PYLON_DEVICE_HANDLE hDev;

        /* Set up the controls and events to be used and update the device list. */
        public MainForm()
        {
            InitializeComponent();

            /* Register for the events of the image provider needed for proper operation. */
            m_imageProvider.GrabErrorEvent += new ImageProvider.GrabErrorEventHandler(OnGrabErrorEventCallback);
            m_imageProvider.DeviceRemovedEvent += new ImageProvider.DeviceRemovedEventHandler(OnDeviceRemovedEventCallback);
            m_imageProvider.DeviceOpenedEvent += new ImageProvider.DeviceOpenedEventHandler(OnDeviceOpenedEventCallback);
            m_imageProvider.DeviceClosedEvent += new ImageProvider.DeviceClosedEventHandler(OnDeviceClosedEventCallback);
            m_imageProvider.GrabbingStartedEvent += new ImageProvider.GrabbingStartedEventHandler(OnGrabbingStartedEventCallback);
            m_imageProvider.ImageReadyEvent += new ImageProvider.ImageReadyEventHandler(OnImageReadyEventCallback);
            m_imageProvider.GrabbingStoppedEvent += new ImageProvider.GrabbingStoppedEventHandler(OnGrabbingStoppedEventCallback);

            /* Provide the controls in the lower left area with the image provider object. */
            sliderGain.MyImageProvider = m_imageProvider;
            sliderExposureTime.MyImageProvider = m_imageProvider;
            sliderHeight.MyImageProvider = m_imageProvider;
            sliderWidth.MyImageProvider = m_imageProvider;
            comboBoxPixelFormat.MyImageProvider = m_imageProvider;

            /* Update the list of available devices in the upper left area. */
            UpdateDeviceList();

            /* Enable the tool strip buttons according to the state of the image provider. */
            EnableButtons(m_imageProvider.IsOpen, false);
         //   hDev = Pylon.CreateDeviceByIndex(0);
            aw = new AviWriter();
        }

        /* Handles the click on the single frame button. */
        private void toolStripButtonOneShot_Click(object sender, EventArgs e)
        {
            OneShot(); /* Starts the grabbing of one image. */
        }

        private void get_single_frame(object sender, EventArgs e)
        {
            TriggeredShot(); /* Starts the grabbing of one image. */
        }

        /* Handles the click on the continuous frame button. */
        private void toolStripButtonContinuousShot_Click(object sender, EventArgs e)
        {
            /*ContinuousShot(); /* Start the grabbing of images until grabbing is stopped. */
            my_trigger = new Multimedia.Timer();
            my_trigger.Resolution = 0;
            my_trigger.Period = (int)trigger_period.Value;
            my_trigger.Tick += new EventHandler(get_single_frame);
            my_trigger.Start();
        }

        /* Handles the click on the stop frame acquisition button. */
        private void toolStripButtonStop_Click(object sender, EventArgs e)
        {
            if (is_recording == true)
            {
                toolStripButtonRecord_Click(sender, e);
            }
            Stop(); /* Stops the grabbing of images. */
        }

        /* Handles the event related to the occurrence of an error while grabbing proceeds. */
        private void OnGrabErrorEventCallback(Exception grabException, string additionalErrorMessage)
        {
            if (InvokeRequired)
            {
                /* If called from a different thread, we must use the Invoke method to marshal the call to the proper thread. */
                BeginInvoke(new ImageProvider.GrabErrorEventHandler(OnGrabErrorEventCallback), grabException, additionalErrorMessage);
                return;
            }
            ShowException(grabException, additionalErrorMessage);
        }

        /* Handles the event related to the removal of a currently open device. */
        private void OnDeviceRemovedEventCallback()
        {
            if (InvokeRequired)
            {
                /* If called from a different thread, we must use the Invoke method to marshal the call to the proper thread. */
                BeginInvoke(new ImageProvider.DeviceRemovedEventHandler(OnDeviceRemovedEventCallback));
                return;
            }
            /* Disable the buttons. */
            EnableButtons(false, false);
            /* Stops the grabbing of images. */
            Stop();
            /* Close the image provider. */
            CloseTheImageProvider();
            /* Since one device is gone, the list needs to be updated. */
            UpdateDeviceList();
        }

        /* Handles the event related to a device being open. */
        private void OnDeviceOpenedEventCallback()
        {
            if (InvokeRequired)
            {
                /* If called from a different thread, we must use the Invoke method to marshal the call to the proper thread. */
                BeginInvoke(new ImageProvider.DeviceOpenedEventHandler(OnDeviceOpenedEventCallback));
                return;
            }
            /* The image provider is ready to grab. Enable the grab buttons. */
            EnableButtons(true, false);
        }

        /* Handles the event related to a device being closed. */
        private void OnDeviceClosedEventCallback()
        {
            if (InvokeRequired)
            {
                /* If called from a different thread, we must use the Invoke method to marshal the call to the proper thread. */
                BeginInvoke(new ImageProvider.DeviceClosedEventHandler(OnDeviceClosedEventCallback));
                return;
            }
            /* The image provider is closed. Disable all buttons. */
            EnableButtons(false, false);
        }

        /* Handles the event related to the image provider executing grabbing. */
        private void OnGrabbingStartedEventCallback()
        {
            if (InvokeRequired)
            {   
                /* If called from a different thread, we must use the Invoke method to marshal the call to the proper thread. */
                BeginInvoke(new ImageProvider.GrabbingStartedEventHandler(OnGrabbingStartedEventCallback));
                return;
            }
            /* The image provider is grabbing. Disable the grab buttons. Enable the stop button. */
            EnableButtons(false, true);
        }

        /* Handles the event related to an image having been taken and waiting for processing. */
        private void OnImageReadyEventCallback()
        {
            if (InvokeRequired)
            {
                /* Suspend the grab thread for a while to avoid blocking the computer by using up all processor resources. */
                System.Threading.Thread.Sleep(20); /* This is only required for this sample. */
                /* If called from a different thread, we must use the Invoke method to marshal the call to the proper thread. */
                BeginInvoke(new ImageProvider.ImageReadyEventHandler(OnImageReadyEventCallback));
                return;
            }

            try
            {
                /* Acquire the image from the image provider. */
                ImageProvider.Image image = m_imageProvider.GetCurrentImage();

                /* Check if the image has been removed in the meantime. */
                if (image != null)
                {
                    /* Check if the image is compatible with the currently used bitmap. */
                    if (BitmapFactory.IsCompatible(m_bitmap, image.Width, image.Height, image.Color))
                    {
                        /* Update the bitmap with the image data. */
                        BitmapFactory.UpdateBitmap(m_bitmap, image.Buffer, image.Width, image.Height, image.Color);
                        /* To show the new image, request the display control to update itself. */
                        pictureBox.Refresh();
                    }
                    else /* A new bitmap is required. */
                    {
                        BitmapFactory.CreateBitmap(ref m_bitmap, image.Width, image.Height, image.Color);
                        BitmapFactory.UpdateBitmap(m_bitmap, image.Buffer, image.Width, image.Height, image.Color);
                        /* Provide the display control with the new bitmap. This action automatically updates the display. */
                        pictureBox.Image = m_bitmap;
                    }
                    if (is_recording == true)
                    {
                        RotateFlipType t = RotateFlipType.RotateNoneFlipY;
                        m_bitmap.RotateFlip(t);
                        aw.AddFrame(m_bitmap);
                    }

                    /* The processing of the image is done. Release the image buffer. */
                    m_imageProvider.ReleaseImage();
                    /* The buffer can be used for the next image grabs. 
                     If another image is in the output queue it can be acquired now using GetCurrentImage(). */
                }
            }
            catch (Exception e)
            {
                ShowException(e, m_imageProvider.GetLastErrorMessage());
            }
        }
        
        /* Handles the event related to the image provider having stopped grabbing. */
        private void OnGrabbingStoppedEventCallback()
        {
            if (InvokeRequired)
            {
                /* If called from a different thread, we must use the Invoke method to marshal the call to the proper thread. */
                BeginInvoke(new ImageProvider.GrabbingStoppedEventHandler(OnGrabbingStoppedEventCallback));
                return;
            }
            /* The image provider stopped grabbing. Enable the grab buttons. Disable the stop button. */
            EnableButtons(m_imageProvider.IsOpen, false);
        }

        /* Helps to set the states of all buttons. */
        private void EnableButtons(bool canGrab, bool canStop)
        {
            toolStripButtonContinuousShot.Enabled = canGrab;
            toolStripButtonOneShot.Enabled = canGrab;
            toolStripButtonStop.Enabled = canStop;
            toolStripButtonRecord.Enabled = canStop;
           
        }

        /* Stops the image provider and handles exceptions. */
        private void Stop()
        {
            if (my_trigger != null)
            {
                if (my_trigger.IsRunning)
                {
                    my_trigger.Stop();
                    OnGrabbingStoppedEventCallback();
                }
            }
            /* Stop the grabbing. */
            try
            {
                m_imageProvider.Stop();
            }
            catch (Exception e)
            {
                ShowException(e, m_imageProvider.GetLastErrorMessage());
            }
        }

        /* Closes the image provider and handles exceptions. */
        private void CloseTheImageProvider()
        {
            /* Close the image provider. */
            try
            {
                m_imageProvider.Close();
            }
            catch (Exception e)
            {
                ShowException(e, m_imageProvider.GetLastErrorMessage());
            }
        }

        /* Starts the grabbing of one image and handles exceptions. */
        private void OneShot()
        {
            try
            {
                m_imageProvider.OneShot(); /* Starts the grabbing of one image. */
            }
            catch (Exception e)
            {
                ShowException(e, m_imageProvider.GetLastErrorMessage());
            }
        }

        /* Starts the grabbing of a triggered shot */
        private void TriggeredShot()
        {
            try
            {
                m_imageProvider.TriggeredShot(); /* Starts the grabbing of one image. */
            }
            catch (Exception e)
            {
                ShowException(e, m_imageProvider.GetLastErrorMessage());
            }
        }

        /* Starts the grabbing of images until the grabbing is stopped and handles exceptions. */
        private void ContinuousShot()
        {
            try
            {
                m_imageProvider.ContinuousShot(); /* Start the grabbing of images until grabbing is stopped. */
            }
            catch (Exception e)
            {
                ShowException(e, m_imageProvider.GetLastErrorMessage());
            }
        }

        /* Updates the list of available devices in the upper left area. */
        private void UpdateDeviceList()
        {
            try
            {
                /* Clear the device list. */
                deviceListView.Items.Clear();
                /* Ask the device enumerator for a list of devices. */
                List<DeviceEnumerator.Device> list = DeviceEnumerator.EnumerateDevices();
                /* Add each device to the list. */
                foreach (DeviceEnumerator.Device device in list)
                {
                    ListViewItem item = new ListViewItem(device.Name);
                    item.Tag = device; /* Attach the device data. */
                    deviceListView.Items.Add(item);
                }
            }
            catch (Exception e)
            {
                ShowException(e, m_imageProvider.GetLastErrorMessage());
            }
            
        }

        /* Shows exceptions in a message box. */
        private void ShowException(Exception e, string additionalErrorMessage)
        {
            string more = "\n\nLast error message (may not belong to the exception):\n" + additionalErrorMessage;
            MessageBox.Show("Exception caught:\n" + e.Message + (additionalErrorMessage.Length > 0 ? more : ""), "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }

        /* Closes the image provider when the window is closed. */
        private void MainForm_FormClosing(object sender, FormClosingEventArgs ev)
        {
            /* Stops the grabbing of images. */
            Stop();
            /* Close the image provider. */
            CloseTheImageProvider();
        }

        /* Handles the selection of cameras from the list box. The currently open device is closed and the first 
         selected device is opened. */
        private void deviceListView_SelectedIndexChanged(object sender, EventArgs ev)
        {
            /* Close the currently open image provider. */
            /* Stops the grabbing of images. */
            Stop();
            /* Close the image provider. */
            CloseTheImageProvider();


            /* Open the selected image provider. */
            if (deviceListView.SelectedItems.Count > 0)
            {
                /* Get the first selected item. */
                ListViewItem item = deviceListView.SelectedItems[0];
                /* Get the attached device data. */
                DeviceEnumerator.Device device = (DeviceEnumerator.Device)item.Tag;
                try
                {
                    /* Open the image provider using the index from the device data. */
                    m_imageProvider.Open(device.Index);
                    m_imageProvider.GetNodeFromDevice(device.Name);
                    hDev = Pylon.CreateDeviceByIndex(device.Index);

                }
                catch (Exception e)
                {
                    ShowException(e, m_imageProvider.GetLastErrorMessage());
                }
            }
 //          Console.WriteLine(m_imageProvider.get_integerParam("Width").ToString());
 
//            hDev = new PYLON_DEVICE_HANDLE();
  //          Pylon.Initialize();

            /* Enumerate all camera devices. You must call 
            PylonEnumerateDevices() before creating a device. */

/*            if (0 == Pylon.EnumerateDevices())
            {
                throw new Exception("No devices found.");
            }
            */
 

        }

        /* If the F5 key has been pressed update the list of devices. */
        private void deviceListView_KeyDown(object sender, KeyEventArgs ev)
        {
            if (ev.KeyCode == Keys.F5)
            {
                ev.Handled = true;
                /* Update the list of available devices in the upper left area. */
                UpdateDeviceList();
            }
        }

        private void toolStripButtonRecord_Click(object sender, EventArgs e)
        {

            if (is_recording == false)
            {
                saveFileDialog1.Filter = "avi file|*.avi";
                saveFileDialog1.Title = "Save Video File";
                saveFileDialog1.InitialDirectory = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
                saveFileDialog1.FileName = "video_grab_" + System.DateTime.Today.Month.ToString() + "-" + System.DateTime.Today.Day.ToString() + "-" + System.DateTime.Today.Year.ToString();
                saveFileDialog1.ShowDialog();
                if (saveFileDialog1.FileName != "")
                {
                   // frame_rate = (float) m_imageProvider.get_floatParam("ResultingFrameRateAbs");
                    frame_rate = (float) 1 / ((float) my_trigger.Period / (float) 1000.0);
                    Console.WriteLine(frame_rate);
                    uint temp = ((uint)( frame_rate* 333333));
                    aw.Open(saveFileDialog1.FileName, ((uint)(temp)), (int)m_imageProvider.get_integerParam("Width"), (int)m_imageProvider.get_integerParam("Height"));

                    is_recording = true;
                    toolStripButtonRecord.BackColor = System.Drawing.SystemColors.GradientActiveCaption;
             //       cmdRecord.BackColor = System.Drawing.SystemColors.GradientActiveCaption;
              //      this.cmdRecord.UseVisualStyleBackColor = false;
                }
          }
            else
            {
                is_recording = false;
                aw.Close();
                toolStripButtonRecord.BackColor = System.Drawing.SystemColors.Control;
             //   cmdRecord.BackColor = System.Drawing.SystemColors.Control;
            //    this.cmdRecord.UseVisualStyleBackColor = true;
            }
        }

        private void numericUpDown1_ValueChanged(object sender, EventArgs e)
        {
            if (my_trigger != null)
            {
                my_trigger.Period = (int)trigger_period.Value;
            }
        }

    }
}