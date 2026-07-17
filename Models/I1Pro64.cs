using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using I1_Integer = System.Int32;

namespace OGVColorCatcher.Models
{

    public class I1Pro64 : IDisposable
    {
        /// <summary>
        ///   Result enumeration
        /// </summary>
        public enum Result
        {
            eNoError = 0,     /**< no error, success */

            /* wrong usage of functions, wrong mode, parameters, ... Fix the program flow in your app */
            eException = 1,     /**< internal exception */
            eBadBuffer = 2,     /**< size of the buffer is not large enough for the data */
            eInvalidHandle = 9,     /**< I1_DeviceHandle is no longer valid, no device associated to this handle (device unplugged) */
            eInvalidArgument = 10,     /**< a passed method argument is invalid (e.g. NULL) */
            eDeviceNotOpen = 11,     /**< the device is not open. Open device first */
            eDeviceNotConnected = 12,     /**< the device is not physically attached to the computer */
            eDeviceNotCalibrated = 13,     /**< the device has not been calibrated or the calibration has expired */
            eNoDataAvailable = 14,     /**< measurement not triggered, index out of range (in scan mode) */
            eNoMeasureModeSet = 15,     /**< no measure mode has been set */
            eNoReferenceChartLine = 17,     /**< no reference chart line for correlation set */
            eNoSubstrateWhite = 18,     /**< no substrate white reference set */
            eNotLicensed = 19,     /**< function not licensed (available) for this device */
            eDeviceAlreadyOpen = 20,     /**< the device has been opened already */

            /* user device handling error. Instruct user what to do */
            eDeviceAlreadyInUse = 51,     /**< the device is already in use by another application */
            eDeviceCommunicationError = 52,     /**< a USB communication error occurred, try to disconnect and reconnect the device */
            eUSBPowerProblem = 53,     /**< a USB power problem was detected. If you run the instrument on a self-powered USB hub, check the hub's power supply. If you run the instrument on a bus-powered USB hub, reduce the number of devices on the hub or switch to a self-powered USB hub */
            eNotOnWhiteTile = 54,     /**< calibration failed because the device might not be on its white tile or the protective white tile slider is closed */

            eStripRecognitionFailed = 60,     /**< recognition is enabled and failed. Scan again */
            eChartCorrelationFailed = 61,     /**< could not map scanned data to the reference chart. Scan again */
            eInsufficientMovement = 62,     /**< distance of movement too short on the i1Pro2 ruler during scan. The device didn't move. Scan again */
            eExcessiveMovement = 63,     /**< distance of movement exceeds licensed i1Pro2 ruler length. Print shorter patch lines */
            eEarlyScanStart = 64,     /**< missed patches at the beginning of a scan. The user must wait at least 500 milliseconds between pressing the button and starting to move the device */
            eUserTimeout = 65,     /**< the user action took too long, try again quicker */
            eIncompleteScan = 66,     /**< the user did not scan over all patches */
            eDeviceNotMoved = 67,     /**< the user did not move during scan measurement (no Zebra Ruler data received). May be the user lifted the device */

            /* device may be corrupt. Tell user she/he should contact customer support */
            eDeviceCorrupt = 71,     /**< an internal diagnostic detected a problem with the instruments data. Please check using i1Diagnostics to obtain more information */
            eWavelengthShift = 72,      /**< an internal diagnostic of wavelength shift detected a problem with spectral sensor. Please check with i1Diagnostics to obtain more information */

            eButtonIsPressed = 1000,
            eButtonNotPressed = 1001
        }


        /// <summary>
        ///   I1 Event enumeration
        /// </summary>
        private enum I1Event
        {
            eI1ProArrival = 0x11,  //< i1Pro plugged-in
            eI1ProDeparture = 0x12,  //< i1Pro unplugged 

            eI1ProButtonPressed = 0x01,  //< measure button pressed on i1Pro
            eI1ProScanReadyToMove = 0x02,  //< in scan mode with Tungsten filament lamp: i1Pro can be moved now. Use this event to beep, flash screen, etc. to signal user that he now can start to move the device
            eI1ProLampRestore = 0x03   //< calibration detected a nonstandard lamp condition. Restoring the standard lamp condition adds around 120 seconds to the calibration process. If this event is emitted, inform user that calibration will take longer than usual #I1_Calibrate()
        }


        /// <summary>
        ///   Measurement mode enumeration
        /// </summary>
        public enum MeasurementModeType
        {
            eUndefined,
            eReflectanceSpot,
            eReflectanceScan,
            eEmissionSpot,
            eAmbientSpot,
            eAmbientScan,
            eDualReflectanceSpot,
            eDualReflectanceScan
        }

        public enum IlluminantConditionType
        {
            eM0,
            eM1,
            eM2
        }

        private static readonly char DELIMITER = ';';

        private static readonly string I1_SDK_VERSION = "SDKVersion";

        private static readonly string I1_YES = "1";                           // value - for various options (global and device specific options) 
        private static readonly string I1_NO = "0";                           // value - for various options (global and device specific options) 

        //DLL info for measurement modes
        private static readonly string I1_AVAILABLE_MEASUREMENT_MODES = "AvailableMeasurementModes";   // key - all available measurement modes. Separated by #I1_VALUE_DELIMITER. Read-only
        private static readonly string I1_MEASUREMENT_MODE = "MeasurementMode";             // key - the active measurement mode. Changing the measurement mode after a measurment will flush the cached results.
        private static readonly string I1_MEASUREMENT_MODE_UNDEFINED = "MeasurementModeUndefined";    // value - the default for #I1_MEASUREMENT_MODE
        private static readonly string I1_REFLECTANCE_SPOT = "ReflectanceSpot";             // value - measurement mode for one spot measurement on a reflective surface
        private static readonly string I1_REFLECTANCE_SCAN = "ReflectanceScan";             // value - measurement mode for a scan on a reflective surface (chart)
        private static readonly string I1_EMISSION_SPOT = "EmissionSpot";                // value - measurement mode for an emission measurement on an emitting probe (display)
        private static readonly string I1_AMBIENT_LIGHT_SPOT = "AmbientLightSpot";            // value - measurement mode for an ambient light measurement
        private static readonly string I1_AMBIENT_LIGHT_SCAN = "AmbientLightScan";            // value - measurement mode for an ambient light scan (flash)
        private static readonly string I1_DUAL_REFLECTANCE_SPOT = "DualReflectanceSpot";         // value - measurement mode for a spot measurement with Tungsten filament lamp and UV Led. Only available for i1Pro RevE devices
        private static readonly string I1_DUAL_REFLECTANCE_SCAN = "DualReflectanceScan";         // value - measurement mode for a two way scan measurement with Tungsten filament lamp and UV Led. Only available for i1Pro RevE devices. Must be performed with i1Pro RevE ruler.

        //DLL info for device functions 
        private static readonly string I1_HAS_UV_LED_KEY = "HasUVLed";                    // key - the device has a UV LED. Read-only. Possible values: I1_YES, I1_NO 
        private static readonly string I1_HAS_UVCUT_FILTER_KEY = "HasUVcutFilter";              // key - the device has a physical UV cut filter. Read-only. Possible values: I1_YES, I1_NO 
        private static readonly string I1_HAS_WAVELENGTH_LED_KEY = "HasWavelengthLed";            // key - the device has a wavelength LED. Read-only. Possible values: I1_YES, I1_NO 
        private static readonly string I1_HAS_ZEBRA_RULER_SENSOR_KEY = "HasZebraRulerSensor";         // key - the device has a Zebra Ruler Sensor. Read-only. Possible values: I1_YES, I1_NO 
        private static readonly string I1_HAS_INDICATOR_LED_KEY = "HasIndicatorLed";             // key - the device has indicator LEDs. Read-only. Possible values: I1_YES, I1_NO 
        private static readonly string I1_HAS_AMBIENT_LIGHT_KEY = "HasAmbientLight";             // key - the device has the ambient light feature. Read-only. Possible values: I1_YES, I1_NO 
        private static readonly string I1_HAS_LOW_RESOLUTION_KEY = "HasLowResolution";            // key - the device has the low resolution feature. Read-only. Possible values: I1_YES, I1_NO 
        private static readonly string I1_MAX_RULER_LENGTH_KEY = "MaxRulerLength";              // key - maximal ruler length in millimeters. Read-only. Possible values: an integer expressed as a string 
        private static readonly string I1_IS_EMISSION_ONLY_KEY = "IsEmssionOnly";               // key - can the i1Monitor device measure emission only? Read-only. Possible values: I1_YES, I1_NO 
        private static readonly string I1_HW_REVISION_KEY = "HWRevision";                  // key - the revision of the hardware device. Read-only. Possible values: A,B,C,D,E,... 
        private static readonly string I1_SUPPLIER_NAME_KEY = "SupplierName";                // key - the name of the branded supplier. Read-only 

        //DLL info for illuminants 
        private const string I1_AVAILABLE_ILLUMINATIONS_KEY = "AvailableIlluminationsKey";              // key - all available illumination mode
        private const string I1_AVAILABLE_RESULT_INDEXES_KEY = "AvailableResultIndexesKey";             // key - all available types of results from measurement (Spectrum/TriStimulus/Density)
        private const string I1_RESULT_INDEX_KEY = "ResultIndexKey";                                    // key - receive the value of Illumination Condition (only for Dual Reflectance)
        private const string I1_ILLUMINATION_CONDITION_M0 = "M0";                                       // value - Condition for M0 light
        private const string I1_ILLUMINATION_CONDITION_M1 = "M1";                                       // value - Condition for M1 light
        private const string I1_ILLUMINATION_CONDITION_M2 = "M2";                                       // value - Condition for M2 light
        //private const string I1_EMISSIVE = "Emissive";                                                  // value - Condition for Emissive light

        //The following static strings are for scan measurement ----------------------------------------
        //DLL info for patches
        //private static readonly string I1_AVAILABLE_PATCH_RECOGNITIONS_KEY = "AvailableRecognitionsKey";
        private static readonly string I1_PATCH_RECOGNITION_KEY = "RecognitionKey";
        // private static readonly string I1_PATCH_RECOGNITION_DISABLED = "RecognitionDisabled";
        private static readonly string I1_PATCH_RECOGNITION_BASIC = "RecognitionBasic";
        // private static readonly string I1_PATCH_RECOGNITION_CORRELATION = "RecognitionCorrelation";
        private static readonly string I1_PATCH_RECOGNITION_POSITION = "RecognitionPosition";
        //private static readonly string I1_PATCH_RECOGNITION_FLASH = "RecognitionFlash";
        //private static readonly string I1_PATCH_RECOGNITION_RECOGNIZED_PATCHES = "RecognitionRecognizedPatches";

        //DLL info for scan direction
        private static readonly string I1_SCAN_DIRECTION_KEY = "ScanDirectionKey";
        private static readonly string I1_SCAN_DIRECTION_FORWARD = "1";
        private static readonly string I1_SCAN_DIRECTION_BACKWARD = "2";
        // private static readonly string I1_SCAN_DIRECTION_UNDEFINED = "0";
        private static readonly string I1_NUMBER_OF_PATCHES_PER_LINE = "PatchesPerLine";
        private static readonly string I1_LAST_SCAN_DIRECTION_KEY = "LastScanDirectionKey";
        private static readonly string I1_LAST_SCAN_RIGHT_TO_LEFT = "-1";
        private static readonly string I1_LAST_SCAN_LEFT_TO_RIGHT = "1";
        //private static readonly string I1_LAST_SCAN_UNDEFINED = "0";

        //private static readonly string I1_SDK_VERSION_MAJOR = "SDKVersionMajor";
        //private static readonly string I1_SDK_VERSION_MINOR = "SDKVersionMinor";
        //private static readonly string I1_SDK_VERSION_REVISION = "SDKVersionRevision";
        //private static readonly string I1_SDK_VERSION_BUILD = "SDKVersionBuild";
        //private static readonly string I1_SDK_VERSION_SUFFIX = "SDKVersionSuffix";
        //private static readonly string I1_LAST_ERROR = "LastError";
        //private static readonly string I1_LAST_ERROR_TEXT = "LastErrorText";
        //private static readonly string I1_LAST_ERROR_NUMBER = "LastErrorNumber";
        private static readonly string I1_ON_MEASUREMENT_SUCCESS_NO_LED_INDICATION = "OnMeasurementSuccessNoLedIndication";

        private static readonly string I1_INDICATOR_LED_KEY = "IndicatorLedKey";
        private static readonly string I1_INDICATOR_LED_MEASUREMENT_SUCCEEDED = "IndicatorLedSucceeded";
        private static readonly string I1_INDICATOR_LED_MEASUREMENT_FAILED = "IndicatorLedFailed";
        //private static readonly string I1_INDICATOR_LED_MEASUREMENT_WRONG_ROW = "IndicatorLedWrongRow";
        private static readonly string I1_INDICATOR_LED_WAIT_FOR_SCAN_LEFT = "IndicatorLedWait4LeftScan";
        private static readonly string I1_INDICATOR_LED_WAIT_FOR_SCAN_RIGHT = "IndicatorLedWait4RightScan";
        private static readonly string I1_INDICATOR_LED_WAIT_FOR_SCAN = "IndicatorLedWait4Scan";
        private static readonly string I1_INDICATOR_LED_OFF = "IndicatorLedOff";
        // private static readonly string I1_INDICATOR_LED_I1IO_POSITION_ACCEPT = "IndicatorLedIOPositionAccept";


        //The following static strings are for density measurement ----------------------------------------
        //DLL info for density modes
        private const string DENSITY_STANDARD_KEY = "Colorimetric.DensityStandard";
        private const string DENSITY_STANDARD_DIN = "DIN";
        private const string DENSITY_STANDARD_DINNB = "DINNB";
        private const string DENSITY_STANDARD_ANSIA = "ANSIA";
        private const string DENSITY_STANDARD_ANSIE = "ANSIE";
        private const string DENSITY_STANDARD_ANSII = "ANSII";
        private const string DENSITY_STANDARD_ANSIT = "ANSIT";
        private const string DENSITY_STANDARD_SPI = "SPI";

        ////DLL info for substrate mode
        private const string WHITE_BASE_KEY = "Colorimetric.WhiteBase";
        private const string WHITE_BASE_ABSOLUTE = "Absolute";
        private const string WHITE_BASE_PAPER = "Paper";
        private const string WHITE_BASE_AUTOMATIC = "Automatic";

        //private const string I1_TIME_SINCE_LAST_CALIBRATION = "TimeSinceLastCalibration";
        //private const string I1_TIME_UNTIL_CALIBRATION_EXPIRE = "TimeUntilCalibrationExpire";
        //private const string I1_MEASURE_COUNT = "MeasureCount";

        //private const string I1_REFERENCE_CHART_COLOR_SPACE_KEY = "ReferenceChartColorSpaceKey";

        private const string ILLUMINATION_KEY = "Colorimetric.Illumination";
        private const string OBSERVER_KEY = "Colorimetric.Observer";
        private const string COLOR_SPACE_KEY = "ColorSpaceDescription.Type";
        //private const string ILLUMINATION_EMISSION = "Emission";

        private const string I1_RESET = "Reset";
        private const string I1_ALL = "All";

        //private const string I1_PRECISION_CALIBRATION_KEY = "PrecisionCalibration";
        private const string COLOR_SPACE_RGB = "RGB";
        private const string COLOR_SPACE_CIELab = "CIELab";


        // private const string ILLUMINATION_D65 = "D65";


        //-----------------------------------------

        private static readonly string I1PRO3_YES = "1";                           // value - for various options (global and device specific options) 
        private static readonly string I1PRO3_NO = "0";                           // value - for various options (global and device specific options) 

        private static readonly string I1PRO3_SDK_VERSION = "SDKVersion";

        // private static readonly string I1PRO3_AVAILABLE_MEASUREMENT_MODES = "AvailableMeasurementModes";   // key - all available measurement modes. Separated by #I1_VALUE_DELIMITER. Read-only
        private static readonly string I1PRO3_MEASUREMENT_MODE = "MeasurementMode";             // key - the active measurement mode. Changing the measurement mode after a measurment will flush the cached results.
        //private static readonly string I1PRO3_MEASUREMENT_MODE_UNDEFINED = "MeasurementModeUndefined";    // value - the default for #I1_MEASUREMENT_MODE
        private static readonly string I1PRO3_REFLECTANCE_SPOT = "ReflectanceSpot";             // value - measurement mode for one spot measurement on a reflective surface
        private static readonly string I1PRO3_REFLECTANCE_SCAN = "ReflectanceScan";             // value - measurement mode for a scan on a reflective surface (chart)
        //private static readonly string I1PRO3_EMISSION_SPOT = "EmissionSpot";                // value - measurement mode for an emission measurement on an emitting probe (display)
        //private static readonly string I1PRO3_AMBIENT_LIGHT_SPOT = "AmbientLightSpot";            // value - measurement mode for an ambient light measurement
        //private static readonly string I1PRO3_AMBIENT_LIGHT_SCAN = "AmbientLightScan";            // value - measurement mode for an ambient light scan (flash)
        //private static readonly string I1PRO3_DUAL_REFLECTANCE_SPOT = "DualReflectanceSpot";         // value - measurement mode for a spot measurement with Tungsten filament lamp and UV Led. Only available for i1Pro RevE devices
        //private static readonly string I1PRO3_DUAL_REFLECTANCE_SCAN = "DualReflectanceScan";         // value - measurement mode for a two way scan measurement with Tungsten filament lamp and UV Led. Only available for i1Pro RevE devices. Must be performed with i1Pro RevE ruler.

        //private static readonly string I1PRO3_HAS_UV_LED_KEY = "HasUVLed";                    // key - the device has a UV LED. Read-only. Possible values: I1_YES, I1_NO 
        //private static readonly string I1PRO3_HAS_UVCUT_FILTER_KEY = "HasUVcutFilter";              // key - the device has a physical UV cut filter. Read-only. Possible values: I1_YES, I1_NO 
        //private static readonly string I1PRO3_HAS_WAVELENGTH_LED_KEY = "HasWavelengthLed";            // key - the device has a wavelength LED. Read-only. Possible values: I1_YES, I1_NO 
        //private static readonly string I1PRO3_HAS_ZEBRA_RULER_SENSOR_KEY = "HasZebraRulerSensor";         // key - the device has a Zebra Ruler Sensor. Read-only. Possible values: I1_YES, I1_NO 
        //private static readonly string I1PRO3_HAS_INDICATOR_LED_KEY = "HasIndicatorLed";             // key - the device has indicator LEDs. Read-only. Possible values: I1_YES, I1_NO 
        //private static readonly string I1PRO3_HAS_AMBIENT_LIGHT_KEY = "HasAmbientLight";             // key - the device has the ambient light feature. Read-only. Possible values: I1_YES, I1_NO 
        //private static readonly string I1PRO3_HAS_LOW_RESOLUTION_KEY = "HasLowResolution";            // key - the device has the low resolution feature. Read-only. Possible values: I1_YES, I1_NO 
        //private static readonly string I1PRO3_MAX_RULER_LENGTH_KEY = "MaxRulerLength";              // key - maximal ruler length in millimeters. Read-only. Possible values: an integer expressed as a string 
        //private static readonly string I1PRO3_IS_EMISSION_ONLY_KEY = "IsEmssionOnly";               // key - can the i1Monitor device measure emission only? Read-only. Possible values: I1_YES, I1_NO 
        //private static readonly string I1PRO3_HW_REVISION_KEY = "HWRevision";                  // key - the revision of the hardware device. Read-only. Possible values: A,B,C,D,E,... 
        //private static readonly string I1PRO3_SUPPLIER_NAME_KEY = "SupplierName";                // key - the name of the branded supplier. Read-only 

        private const string I1PRO3_AVAILABLE_ILLUMINATIONS_KEY = "AvailableIlluminationsKey";
        //private const string I1PRO3_AVAILABLE_RESULT_INDEXES_KEY = "AvailableResultIndexesKey";             // key - all available types of results from measurement (Spectrum/TriStimulus/Density)
        private const string I1PRO3_RESULT_INDEX_KEY = "ResultIndexKey";                                    // key - receive the value of Illumination Condition (only for Dual Reflectance)
        private const string I1PRO3_ILLUMINATION_CONDITION_M0 = "M0";                                       // value - Condition for M0 light
        private const string I1PRO3_ILLUMINATION_CONDITION_M1 = "M1";                                       // value - Condition for M1 light
        private const string I1PRO3_ILLUMINATION_CONDITION_M2 = "M2";                                       // value - Condition for M2 light
        //private const string I1PRO3_EMISSIVE = "Emissive";

        //private static readonly string I1PRO3_AVAILABLE_PATCH_RECOGNITIONS_KEY = "AvailableRecognitionsKey";
        private static readonly string I1PRO3_PATCH_RECOGNITION_KEY = "RecognitionKey";
        //private static readonly string I1PRO3_PATCH_RECOGNITION_DISABLED = "RecognitionDisabled";
        private static readonly string I1PRO3_PATCH_RECOGNITION_BASIC = "RecognitionBasic";
        //private static readonly string I1PRO3_PATCH_RECOGNITION_CORRELATION = "RecognitionCorrelation";
        //private static readonly string I1PRO3_PATCH_RECOGNITION_POSITION = "RecognitionPosition";
        //private static readonly string I1PRO3_PATCH_RECOGNITION_FLASH = "RecognitionFlash";
        //private static readonly string I1PRO3_PATCH_RECOGNITION_RECOGNIZED_PATCHES = "RecognitionRecognizedPatches";

        private static readonly string I1PRO3_INDICATOR_LED_KEY = "IndicatorLedKey";
        private static readonly string I1PRO3_INDICATOR_LED_MEASUREMENT_SUCCEEDED = "IndicatorLedSucceeded";
        //private static readonly string I1PRO3_INDICATOR_LED_MEASUREMENT_FAILED = "IndicatorLedFailed";
        //private static readonly string I1PRO3_INDICATOR_LED_MEASUREMENT_WRONG_ROW = "IndicatorLedWrongRow";
        //private static readonly string I1PRO3_INDICATOR_LED_WAIT_FOR_SCAN_LEFT = "IndicatorLedWait4LeftScan";
        //private static readonly string I1PRO3_INDICATOR_LED_WAIT_FOR_SCAN_RIGHT = "IndicatorLedWait4RightScan";
        //private static readonly string I1PRO3_INDICATOR_LED_WAIT_FOR_SCAN = "IndicatorLedWait4Scan";
        //private static readonly string I1PRO3_INDICATOR_LED_OFF = "IndicatorLedOff";
        //private static readonly string I1PRO3_INDICATOR_LED_I1IO_POSITION_ACCEPT = "IndicatorLedIOPositionAccept";
        private static readonly string I1PRO3_ON_MEASUREMENT_SUCCESS_NO_LED_INDICATION = "OnMeasurementSuccessNoLedIndication";

        //-----------------------------------------

        //I1_ResultType I1_GetDevices(I1_DeviceHandle** devices, I1_UInteger* count);
        [DllImport("i1Pro64.dll", CallingConvention = CallingConvention.Cdecl)]
        private static extern Result I1_GetDevices([MarshalAs(UnmanagedType.LPArray)] ref IntPtr[] devices, ref ulong count);


        // General functions ----------------------------------------

        //I1_ResultType I1_OpenDevice(I1_DeviceHandle devHndl);
        [DllImport("i1Pro64.dll", CallingConvention = CallingConvention.Cdecl)]
        private static extern Result I1_OpenDevice(IntPtr handle);

        //I1_ResultType I1_CloseDevice (I1_DeviceHandle devHndl);
        [DllImport("i1Pro64.dll", CallingConvention = CallingConvention.Cdecl)]
        private static extern Result I1_CloseDevice(IntPtr handle);

        //I1_ResultType I1_GetGlobalOption(const char *key, char *buffer, I1_UInteger *size);
        [DllImport("i1Pro64.dll", CallingConvention = CallingConvention.Cdecl)]
        private static extern Result I1_GetGlobalOption(string key, StringBuilder buffer, ref uint bufferSize);

        //I1_ResultType I1_SetOption(I1_DeviceHandle devHndl, const char *key, const char *value);
        [DllImport("i1Pro64.dll", CallingConvention = CallingConvention.Cdecl)]
        private static extern Result I1_SetOption(IntPtr handle, string key, string value);

        [DllImport("i1Pro64.dll", CallingConvention = CallingConvention.Cdecl)]
        private static extern Result I1_SetGlobalOption(string key, string value);

        //I1_ResultType I1_GetOption(I1_DeviceHandle devHndl, const char *key, char *buffer, I1_UInteger *size);
        [DllImport("i1Pro64.dll", CallingConvention = CallingConvention.Cdecl)]
        private static extern Result I1_GetOption(IntPtr handle, string key, StringBuilder buffer, ref uint bufferSize);

        [DllImport("i1Pro64.dll", CallingConvention = CallingConvention.Cdecl)]
        private static extern Result I1_SetSubstrate(nint handle, float[] substrate);

        // Measurement functions ----------------------------------------
        // Measurement of Spectrum
        //I1_ResultType I1_Calibrate (I1_DeviceHandle devHndl);
        [DllImport("i1Pro64.dll", CallingConvention = CallingConvention.Cdecl)]
        private static extern Result I1_Calibrate(IntPtr handle);

        //I1_ResultType I1_TriggerMeasurement (I1_DeviceHandle devHndl);
        [DllImport("i1Pro64.dll", CallingConvention = CallingConvention.Cdecl)]
        private static extern Result I1_TriggerMeasurement(IntPtr handle);

        //I1_Integer I1_GetNumberOfAvailableSamples (I1_DeviceHandle devHndl);
        [DllImport("i1Pro64.dll", CallingConvention = CallingConvention.Cdecl)]
        private static extern int I1_GetNumberOfAvailableSamples(IntPtr handle);

        //I1_ResultType I1_GetSpectrum(I1_DeviceHandle devHndl, float spectrum[SPECTRUM_SIZE], I1_Integer index);
        [DllImport("i1Pro64.dll", CallingConvention = CallingConvention.Cdecl)]
        private static extern Result I1_GetSpectrum(IntPtr handle, float[] spectrum, int index);

        //I1_ResultType I1_GetTriStimulus(IntPtr handle, float[] tristimulus, int index);
        [DllImport("i1Pro64.dll", CallingConvention = CallingConvention.Cdecl)]
        private static extern Result I1_GetTriStimulus(IntPtr handle, float[] tristimulus, int index);

        // Measurement of Density
        [DllImport("i1Pro64.dll", CallingConvention = CallingConvention.Cdecl)]
        private static extern Result I1_GetDensities(IntPtr handle, float[] densities, I1_Integer index, I1_Integer v);

        [DllImport("i1Pro64.dll", CallingConvention = CallingConvention.Cdecl)]
        private static extern Result I1_GetDensity(IntPtr handle, float[] density, I1_Integer index);



        [DllImport("i1Pro64.dll", CallingConvention = CallingConvention.Cdecl)]
        private static extern Result I1_GetButtonStatusD(IntPtr handle);
        // Callback functions



        //typedef void (I1_CALLING_CONVENTION *FPtr_I1_DeviceEventHandler)(I1_DeviceHandle devHndl, I1_DeviceEvent event, void *context);
        public delegate void DeviceEventCallback(IntPtr device, uint eventId, IntPtr context);

        //FPtr_I1_DeviceEventHandler I1_RegisterDeviceEventHandler (FPtr_I1_DeviceEventHandler handler, void* context);
        [DllImport("i1Pro64.dll", CallingConvention = CallingConvention.Cdecl)]
        private static extern DeviceEventCallback I1_RegisterDeviceEventHandler(DeviceEventCallback deviceEventFunction, IntPtr context);

        //-----------------------------------------------------

        //I1_ResultType I1_GetDevices(I1_DeviceHandle** devices, I1_UInteger* count);
        [DllImport("i1Pro364.dll", CallingConvention = CallingConvention.Cdecl)]
        private static extern Result I1PRO3_GetDevices([MarshalAs(UnmanagedType.LPArray)] ref IntPtr[] devices, ref ulong count);


        // General functions

        //I1_ResultType I1_OpenDevice(I1_DeviceHandle devHndl);
        [DllImport("i1Pro364.dll", CallingConvention = CallingConvention.Cdecl)]
        private static extern Result I1PRO3_OpenDevice(IntPtr handle);

        //I1_ResultType I1_CloseDevice (I1_DeviceHandle devHndl);
        [DllImport("i1Pro364.dll", CallingConvention = CallingConvention.Cdecl)]
        private static extern Result I1PRO3_CloseDevice(IntPtr handle);

        //I1_ResultType I1_GetGlobalOption(const char *key, char *buffer, I1_UInteger *size);
        [DllImport("i1Pro364.dll", CallingConvention = CallingConvention.Cdecl)]
        private static extern Result I1PRO3_GetGlobalOption(string key, StringBuilder buffer, ref uint bufferSize);

        //I1_ResultType I1_SetOption(I1_DeviceHandle devHndl, const char *key, const char *value);
        [DllImport("i1Pro364.dll", CallingConvention = CallingConvention.Cdecl)]
        private static extern Result I1PRO3_SetOption(IntPtr handle, string key, string value);

        //I1_ResultType I1_GetOption(I1_DeviceHandle devHndl, const char *key, char *buffer, I1_UInteger *size);
        [DllImport("i1Pro364.dll", CallingConvention = CallingConvention.Cdecl)]
        private static extern Result I1PRO3_GetOption(IntPtr handle, string key, StringBuilder buffer, ref uint bufferSize);

        [DllImport("i1Pro364.dll", CallingConvention = CallingConvention.Cdecl)]
        private static extern Result I1PRO3_SetSubstrate(nint handle, float[] substrate);

        // Measurement functions

        [DllImport("i1Pro364.dll", CallingConvention = CallingConvention.Cdecl)]
        private static extern Result I1PRO3_GetDensities(IntPtr handle, float[] densities, I1_Integer index, I1_Integer v);


        //I1_ResultType I1_Calibrate (I1_DeviceHandle devHndl);
        [DllImport("i1Pro364.dll", CallingConvention = CallingConvention.Cdecl)]
        private static extern Result I1PRO3_Calibrate(IntPtr handle);

        //I1_ResultType I1_TriggerMeasurement (I1_DeviceHandle devHndl);
        [DllImport("i1Pro364.dll", CallingConvention = CallingConvention.Cdecl)]
        private static extern Result I1PRO3_TriggerMeasurement(IntPtr handle);

        //I1_Integer I1_GetNumberOfAvailableSamples (I1_DeviceHandle devHndl);
        [DllImport("i1Pro364.dll", CallingConvention = CallingConvention.Cdecl)]
        private static extern int I1PRO3_GetNumberOfAvailableSamples(IntPtr handle);

        //I1_ResultType I1_GetSpectrum(I1_DeviceHandle devHndl, float spectrum[SPECTRUM_SIZE], I1_Integer index);
        [DllImport("i1Pro364.dll", CallingConvention = CallingConvention.Cdecl)]
        private static extern Result I1PRO3_GetSpectrum(IntPtr handle, float[] spectrum, int index);

        [DllImport("i1Pro364.dll", CallingConvention = CallingConvention.Cdecl)]
        private static extern Result I1PRO3_GetTriStimulus(IntPtr handle, float[] tristimulus, int index);
        // Callback functions

        //FPtr_I1_DeviceEventHandler I1_RegisterDeviceEventHandler (FPtr_I1_DeviceEventHandler handler, void* context);
        [DllImport("i1Pro364.dll", CallingConvention = CallingConvention.Cdecl)]
        private static extern DeviceEventCallback I1PRO3_RegisterDeviceEventHandler(DeviceEventCallback deviceEventFunction, IntPtr context);

        //-----------------------------------------------------

        //-----------------------------------------------------
        //  Events
        //-----------------------------------------------------
        /// <summary>
        /// Occurs when an instrument gets connected
        /// </summary>
        /// <param name="device">The connected device</param>
        public delegate void DeviceConnectedHandler(I1Pro64 device);
        public static event DeviceConnectedHandler DeviceConnected;

        /// <summary>
        /// Occurs when an instrument gets disconnected
        /// </summary>
        /// <param name="device">The disconnected device</param>
        public delegate void DeviceDisconnectedHandler(I1Pro64 device);
        public static event DeviceDisconnectedHandler DeviceDisconnected;

        /// <summary>
        /// Occurs when an instrument button is pressed.
        /// </summary>
        /// <param name="device">The device which's button was pressed</param>
        public delegate void ButtonPressedHandler(I1Pro64 device);
        public event ButtonPressedHandler ButtonPressed;

        /// <summary>
        /// Occurs when the instrument is ready for scanning
        /// </summary>
        /// <param name="device">The device which is ready for scanning</param>
        public delegate void ScanReadyHandler(I1Pro64 device);
        public event ScanReadyHandler ScanReady;

        /// <summary>
        /// Occurs when the lamp of an instrument was restored
        /// </summary>
        /// <param name="device">The device which's lamp was restored</param>
        public delegate void LampRestoredHandler(I1Pro64 device);
        public event LampRestoredHandler LampRestored;


        /// <summary>
        ///  Log entry type
        /// </summary>
        public enum LogType
        {
            eNormal,
            eError
        }


        /// <summary>
        /// Log event
        /// A log action occured.
        /// <param name="type">Type of the entry (Normal, Error)</param>
        /// <param name="logEntry">The log entry</param>
        /// </summary>
        public delegate void LogEventHandler(LogType type, string logEntry);
        public static event LogEventHandler LogEvent;




        public static int SpectrumSize { get { return 36; } }

        public static int TristimulusSize { get { return 3; } }


        private static bool sdkInitialized = false;
        private static Dictionary<IntPtr, I1Pro64> deviceMap = new Dictionary<IntPtr, I1Pro64>();

        private static DeviceEventCallback callbackFunction;


        /// <summary>
        /// SDK initialization function. 
        /// Gets called automatically from ListDevices.
        /// Multiple calls to SetupSDK are safe.
        /// </summary>
        public static void SetupSDK()
        {
            if (!sdkInitialized)
            {
                callbackFunction = new DeviceEventCallback(DeviceEventHandler);
                IntPtr context = new IntPtr();
                DeviceEventCallback oldCallback = I1PRO3_RegisterDeviceEventHandler(callbackFunction, context);
                oldCallback = I1_RegisterDeviceEventHandler(callbackFunction, context);

                sdkInitialized = true;
            }
        }

        /// <summary>
        /// Get the version of the SDK.
        /// </summary>
        public static string SDKVersion
        {
            get
            {
                if (!isPro3)
                {
                    return GetGlobalOption(I1_SDK_VERSION);
                }
                else if (isPro3)
                {
                    return GetGlobalOption(I1PRO3_SDK_VERSION);

                }
                return null;
            }
        }

        /// <summary>
        /// Writing a log entry and call the LogEvent.
        /// </summary>
        /// <param name="type">Type of the log event (normal, error)</param>
        /// <param name="logEntry">The log entry</param>
        private static void WriteLog(LogType type, string logEntry)
        {
            if (LogEvent != null)
            {
                LogEvent(type, logEntry);
            }
        }

        /// <summary>
        /// Access to a global options. 
        /// </summary>
        /// <param name="key">The global options key</param>
        /// <returns>The option-value of the given key</returns>
        private static string GetGlobalOption(string key)
        {
            StringBuilder buffer = new StringBuilder(10000);
            if (!isPro3)
            {
                UInt32 bufferSize = (UInt32)buffer.Capacity;
                Result result = I1_GetGlobalOption(key, buffer, ref bufferSize);
                if (result == Result.eBadBuffer)
                {
                    buffer.Capacity = (int)bufferSize;
                    result = I1_GetGlobalOption(key, buffer, ref bufferSize);
                }
                HandleResult(result);
            }
            else if (isPro3)
            {
                UInt32 bufferSize = (UInt32)buffer.Capacity;
                Result result = I1PRO3_GetGlobalOption(key, buffer, ref bufferSize);
                if (result == Result.eBadBuffer)
                {
                    buffer.Capacity = (int)bufferSize;
                    result = I1PRO3_GetGlobalOption(key, buffer, ref bufferSize);
                }
                HandleResult(result);
            }

            return buffer.ToString();
        }


        /// <summary>
        /// Internal handler of I1Pro events. Events from the C-SDK arrive here and are forwarded to the respective event handlers.
        /// </summary>
        /// <param name="handle">Device handle</param>
        /// <param name="eventId">Event identification (see I1Event)</param>
        /// <param name="context">Context associated with the event</param>
        private static void DeviceEventHandler(IntPtr handle, uint eventId, IntPtr context)
        {
            switch ((I1Event)eventId)
            {
                case I1Event.eI1ProArrival:
                    try
                    {
                        I1Pro64 newDevice = CreateDevice(handle);
                        {
                            WriteLog(LogType.eNormal, "Device connected " + newDevice.Name);
                            if (DeviceConnected != null)
                            {
                                DeviceConnected(newDevice);
                            }
                        }
                    }
                    catch (Exception)
                    {
                    }
                    break;
                default:
                    if (deviceMap.ContainsKey(handle))
                    {
                        I1Pro64 device = deviceMap[handle];
                        switch ((I1Event)eventId)
                        {
                            case I1Event.eI1ProDeparture:
                                //WriteLog(null, "Device disconnected: " + device.Name);
                                deviceMap.Remove(handle);
                                if (DeviceDisconnected != null)
                                {
                                    DeviceDisconnected(device);
                                }
                                break;
                            case I1Event.eI1ProButtonPressed:
                                WriteLog(LogType.eNormal, "Button pressed: " + device.Name);
                                if (device.ButtonPressed != null)
                                {
                                    device.ButtonPressed(device);
                                }
                                break;
                            case I1Event.eI1ProScanReadyToMove:
                                WriteLog(LogType.eNormal, "Scan - Ready to move: " + device.Name);
                                if (device.ScanReady != null)
                                {
                                    device.ScanReady(device);
                                }
                                break;
                            case I1Event.eI1ProLampRestore:
                                if (device.LampRestored != null)
                                {
                                    device.LampRestored(device);
                                }
                                break;

                        }
                    }
                    break;
            }
        }

        public static bool isPro3 { get; set; }





        /// <summary>
        /// Access to the currently connected devices
        /// </summary>
        /// <returns>The available devices</returns>
        public static List<I1Pro64> LoadDevices()
        {
            SetupSDK();
            WriteLog(LogType.eNormal, "Searching devices");

            deviceMap.Clear();
            ulong count = 0;

            IntPtr[] deviceHdlArray = null;
            IntPtr[] deviceHdlArray1 = null;

            Result result = I1Pro64.I1_GetDevices(ref deviceHdlArray1, ref count);
            HandleResult(result);

            ulong countPro3 = 0;

            Result resultPro3 = I1Pro64.I1PRO3_GetDevices(ref deviceHdlArray, ref countPro3);
            HandleResult(resultPro3);

            List<I1Pro64> devices = new List<I1Pro64>();
            List<I1Pro64> devices3 = new List<I1Pro64>();
            if (count > 0)
            {
                isPro3 = false;
                //deviceValue = 12;
                foreach (var deviceHandle in deviceHdlArray1)
                {
                    try
                    {
                        devices.Add(CreateDevice(deviceHandle));
                    }
                    catch (Exception)
                    {
                    }
                }
            }

            //deviceMap.Clear();

            if (countPro3 > 0)
            {
                isPro3 = true;
                //deviceValue = 3;
                foreach (var deviceHandle in deviceHdlArray)
                {
                    try
                    {
                        devices.Add(CreateDevice(deviceHandle));
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine(ex);
                    }

                }
                //return devices3;
            }

            WriteLog(LogType.eNormal, String.Format("Devices found: {0}", devices.Count));
            return devices;
        }

        /// <summary>
        /// Setup an I1Pro64 device from the given handle.
        /// </summary>
        /// <param name="deviceHandle">The device handle</param>
        /// <returns>The new I1Pro64 device</returns>
        private static I1Pro64 CreateDevice(IntPtr deviceHandle)
        {
            using (I1Pro64 device = new I1Pro64(deviceHandle))
            {
                device.Open();
                deviceMap.Add(deviceHandle, device);
                return device;
            }
        }

        /// <summary>
        /// Checks the result of an i1 operation and throws an I1Exception in case of an error
        /// </summary>
        /// <param name="result">The result from the C-SDK</param>
        private static void HandleResult(Result result)
        {
            if (result == Result.eNoError)
            {
                return;
            }

            WriteLog(LogType.eError, String.Format("Error: {0}", result));
            switch (result)
            {
                case Result.eNoError: return;
                case Result.eException: throw new I1Exception(result, "Internal exception");
                case Result.eBadBuffer: throw new I1Exception(result, "Size of the buffer is not large enough for the data");
                case Result.eInvalidHandle: throw new I1Exception(result, "I1_DeviceHandle is no longer valid. No device associated to this handle (device unplugged)");
                case Result.eInvalidArgument: throw new I1Exception(result, "A passed method argument is invalid (e.g. NULL)");
                case Result.eDeviceNotOpen: throw new I1Exception(result, "The device is not open. Open device first");
                case Result.eDeviceNotConnected: throw new I1Exception(result, "The device is not physically attached to the computer");
                case Result.eDeviceNotCalibrated: throw new I1Exception(result, "The device has not been calibrated or the calibration has expired");
                case Result.eNoDataAvailable: throw new I1Exception(result, "Measurement not triggered, index out of range (in scan mode)");
                case Result.eNoMeasureModeSet: throw new I1Exception(result, "No measure mode has been set");
                case Result.eNoReferenceChartLine: throw new I1Exception(result, "No reference chart line for correlation set");
                case Result.eNoSubstrateWhite: throw new I1Exception(result, "No substrate white reference set");
                case Result.eNotLicensed: throw new I1Exception(result, "Function not licensed (available) for this device");
                case Result.eDeviceAlreadyOpen: throw new I1Exception(result, "The device has been opened already");

                case Result.eDeviceAlreadyInUse: throw new I1Exception(result, "The device is already in use by another application");
                case Result.eDeviceCommunicationError: throw new I1Exception(result, "A USB communication error occurred, try to disconnect and reconnect the device");
                case Result.eUSBPowerProblem: throw new I1Exception(result, "A USB power problem was detected. If you run the instrument on a self-powered USB hub, check the hub's power supply. If you run the instrument on a bus-powered USB hub, reduce the number of devices on the hub or switch to a self-powered USB hub");
                case Result.eNotOnWhiteTile: throw new I1Exception(result, "Calibration failed because the device might not be on its white tile or the protective white tile slider is closed");

                case Result.eStripRecognitionFailed: throw new I1Exception(result, "Recognition is enabled and failed. Scan again");
                case Result.eChartCorrelationFailed: throw new I1Exception(result, "Could not map scanned data to the reference chart. Scan again");
                case Result.eInsufficientMovement: throw new I1Exception(result, "Distance of movement too short on the i1Pro2 ruler during scan. The device didn't move. Scan again");
                case Result.eExcessiveMovement: throw new I1Exception(result, "Distance of movement exceeds licensed i1Pro2 ruler length. Print shorter patch lines");
                case Result.eEarlyScanStart: throw new I1Exception(result, "Missed patches at the beginning of a scan. The user must wait at least 500 milliseconds between pressing the button and starting to move the device");
                case Result.eUserTimeout: throw new I1Exception(result, "The user action took too long, try again quicker");
                case Result.eIncompleteScan: throw new I1Exception(result, "The user did not scan over all patches");
                case Result.eDeviceNotMoved: throw new I1Exception(result, "The user did not move during scan measurement (no Zebra Ruler data received). May be the user lifted the device");

                case Result.eDeviceCorrupt: throw new I1Exception(result, "An internal diagnostic detected a problem with the instruments data. Please check using i1Diagnostics to obtain more information");
                case Result.eWavelengthShift: throw new I1Exception(result, "An internal diagnostic of wavelength shift detected a problem with spectral sensor. Please check with i1Diagnostics to obtain more information");

                case Result.eButtonIsPressed: throw new I1Exception(result, "i1Pro button is pressed");
                case Result.eButtonNotPressed: throw new I1Exception(result, "i1Pro button is not pressed");

                default: throw new I1Exception(result, "Unknown error");
            }
        }

        //-----------------------------------------
        //-----------------------------------------
        //-----------------------------------------

        private static Dictionary<MeasurementModeType, string> measurementModeDictionary;
        /// <summary>
        /// Mapping of Measurement mode to measurement mode strings.
        /// </summary>
        private static Dictionary<MeasurementModeType, string> MeasurementModeDictionary
        {
            get
            {
                if (measurementModeDictionary == null)
                {
                    measurementModeDictionary = new Dictionary<MeasurementModeType, string>();
                    measurementModeDictionary.Add(MeasurementModeType.eUndefined, I1_MEASUREMENT_MODE_UNDEFINED);
                    measurementModeDictionary.Add(MeasurementModeType.eReflectanceSpot, I1_REFLECTANCE_SPOT);
                    measurementModeDictionary.Add(MeasurementModeType.eReflectanceScan, I1_REFLECTANCE_SCAN);
                    measurementModeDictionary.Add(MeasurementModeType.eEmissionSpot, I1_EMISSION_SPOT);
                    measurementModeDictionary.Add(MeasurementModeType.eAmbientSpot, I1_AMBIENT_LIGHT_SPOT);
                    measurementModeDictionary.Add(MeasurementModeType.eAmbientScan, I1_AMBIENT_LIGHT_SCAN);
                    measurementModeDictionary.Add(MeasurementModeType.eDualReflectanceSpot, I1_DUAL_REFLECTANCE_SPOT);
                    measurementModeDictionary.Add(MeasurementModeType.eDualReflectanceScan, I1_DUAL_REFLECTANCE_SCAN);
                }
                return measurementModeDictionary;
            }
        }

        private static Dictionary<IlluminantConditionType, string> illuminantConditionDictionary;
        private static Dictionary<IlluminantConditionType, string> IlluminantConditionDictionary
        {
            get
            {
                if (illuminantConditionDictionary == null)
                {
                    illuminantConditionDictionary = new Dictionary<IlluminantConditionType, string>();
                    illuminantConditionDictionary.Add(IlluminantConditionType.eM0, I1_ILLUMINATION_CONDITION_M0);
                    illuminantConditionDictionary.Add(IlluminantConditionType.eM1, I1_ILLUMINATION_CONDITION_M1);
                    illuminantConditionDictionary.Add(IlluminantConditionType.eM2, I1_ILLUMINATION_CONDITION_M2);
                }
                return illuminantConditionDictionary;
            }
        }

        //-----------------------------------------
        //-----------------------------------------
        //-----------------------------------------

        private IntPtr Handle { get; set; }

        public string SerialNumber { get; private set; }
        public string DeviceType { get; private set; }

        public bool IsOpen { get; private set; }


        /// <summary>
        /// Get the device name (type + serial number)
        /// </summary>
        public string Name
        {
            get
            {
                StringBuilder nameBuilder = new StringBuilder();
                nameBuilder.Append(DeviceType);
                if (SerialNumber != "")
                {
                    if (nameBuilder.Length > 0)
                    {
                        nameBuilder.Append(" - ");
                    }
                    nameBuilder.Append(SerialNumber);
                }

                if (nameBuilder.Length == 0)
                {
                    nameBuilder.Append(Handle.ToString());
                }

                return nameBuilder.ToString();
            }
        }

        /// <summary>
        /// Constructor of an I1Pro64 device
        /// </summary>
        /// <param name="handle">The instrument handle returned from the C-SDK</param>
        private I1Pro64(IntPtr handle)
        {
            if (Environment.Is64BitProcess)
            {
                if (handle.ToInt64() == 0)
                {
                    throw new I1Exception(Result.eInvalidHandle, "Invalid handle");
                }
            }
            else
            {
                if (handle.ToInt32() == 0)
                {
                    throw new I1Exception(Result.eInvalidHandle, "Invalid handle");
                }
            }
            this.Handle = handle;
            this.DeviceType = "";
            this.SerialNumber = "";
            this.IsOpen = false;
        }

        /// <summary>
        /// Dispose function for freeing the device
        /// </summary>
        public void Dispose()
        {
            Close();
        }

        /// <summary>
        /// Open the device for further access
        /// </summary>
        public void Open()
        {
            Result result = I1PRO3_OpenDevice(Handle);

            if (result == Result.eNoError || result == Result.eDeviceAlreadyOpen)
            {
                HandleResult(result);
                IsOpen = true;
                isPro3 = true;
                DeviceType = GetOption("DeviceTypeKey");
                SerialNumber = GetOption("SerialNumber");
                return;
            }

            // Se falhou com erro como "não conectado" ou "não suportado", tenta o I1 normal
            result = I1_OpenDevice(Handle);

            if (result == Result.eNoError || result == Result.eDeviceAlreadyOpen)
            {
                HandleResult(result);
                IsOpen = true;
                isPro3 = false;
                DeviceType = GetOption("DeviceTypeKey");
                SerialNumber = GetOption("SerialNumber");
                return;
            }

            // Se ambos falharem, lança exceção com o último erro
            HandleResult(result);
        }



        /// <summary>
        /// Close the device. The device stays valid and can be reopened.
        /// </summary>
        public void Close()
        {
            if (this.IsOpen)
            {
                if (isPro3)
                {
                    Result result = I1PRO3_CloseDevice(Handle);
                    HandleResult(result);
                    IsOpen = false;
                }
                else if (!isPro3)
                {
                    Result result = I1_CloseDevice(Handle);
                    HandleResult(result);
                    IsOpen = false;
                }
            }
        }

        //-----------------------------------------
        //-----------------------------------------
        //-----------------------------------------

        /// <summary>
        /// Access to device options
        /// </summary>
        /// <param name="key">The device option key</param>
        /// <returns>The option value</returns>
        private string GetOption(string key)
        {
            StringBuilder buffer = new StringBuilder(10000);

            if (!isPro3)
            {
                UInt32 bufferSize = (UInt32)buffer.Capacity;
                Result result = I1_GetOption(Handle, key, buffer, ref bufferSize);
                if (result == Result.eBadBuffer)
                {
                    buffer.Capacity = (int)bufferSize;
                    result = I1_GetOption(Handle, key, buffer, ref bufferSize);
                }
                HandleResult(result);
            }
            else if (isPro3)
            {
                UInt32 bufferSize = (UInt32)buffer.Capacity;
                Result result = I1PRO3_GetOption(Handle, key, buffer, ref bufferSize);
                if (result == Result.eBadBuffer)
                {
                    buffer.Capacity = (int)bufferSize;
                    result = I1PRO3_GetOption(Handle, key, buffer, ref bufferSize);
                }
                HandleResult(result);
            }
            return buffer.ToString();
        }

        /// <summary>
        /// Set/Change a device option
        /// </summary>
        /// <param name="key">The option key</param>
        /// <param name="value">The new option value</param>
        private void SetOption(string key, string value)
        {
            if (!isPro3)
            {
                Result result = I1_SetOption(Handle, key, value);
                HandleResult(result);
            }
            else if (isPro3)
            {
                Result result = I1PRO3_SetOption(Handle, key, value);
                HandleResult(result);
            }

        }


        //-----------------------------------------
        //-----------------------------------------
        //-----------------------------------------

        /// <summary>
        /// Get the available measurement modes
        /// </summary>
        public List<MeasurementModeType> AvailableMeasurementModes
        {
            get
            {
                List<MeasurementModeType> availableMeasurementModes = new List<MeasurementModeType>();
                String result = GetOption(I1_AVAILABLE_MEASUREMENT_MODES);
                foreach (String measurementMode in result.Split(DELIMITER))
                {
                    availableMeasurementModes.Add(MeasurementModeFromString(measurementMode));
                }
                return availableMeasurementModes;
            }
        }

        /// <summary>
        /// Measurement mode converter (string --> Measurement mode)
        /// </summary>
        /// <param name="measurementModeString">The measurement mode string</param>
        /// <returns>The measurement mode</returns>
        private MeasurementModeType MeasurementModeFromString(String measurementModeString)
        {
            if (MeasurementModeDictionary.ContainsValue(measurementModeString))
            {
                return MeasurementModeDictionary.FirstOrDefault(x => x.Value == measurementModeString).Key;
            }
            return MeasurementModeType.eUndefined;
        }

        /// <summary>
        /// Measurement mode converter (Measurement mode --> string)
        /// </summary>
        /// <param name="measurementMode">The measurement mode</param>
        /// <returns>The measurement mode string</returns>
        private String MeasurementModeToString(MeasurementModeType measurementMode)
        {
            if (MeasurementModeDictionary.ContainsKey(measurementMode))
            {
                return MeasurementModeDictionary[measurementMode];
            }
            return I1_MEASUREMENT_MODE_UNDEFINED;
        }

        /// <summary>
        /// Get/Set the device measurement mode
        /// </summary>
        public MeasurementModeType MeasurementMode
        {
            get
            {
                string measurementMode = GetOption(I1_MEASUREMENT_MODE);
                return MeasurementModeFromString(measurementMode);
            }

            set
            {
                if (!MeasurementModeDictionary.ContainsKey(value))
                {
                    throw new I1Exception(Result.eInvalidArgument, "Unknown measurement mode");
                }
                SetOption(I1_MEASUREMENT_MODE, MeasurementModeDictionary[value]);
            }
        }

        //-----------------------------------------
        //-----------------------------------------
        //-----------------------------------------

        public List<IlluminantConditionType> AvailableIlluminantModes
        {
            get
            {
                List<IlluminantConditionType> availableIlluminantCondition = new List<IlluminantConditionType>();
                String result = GetOption(I1_AVAILABLE_ILLUMINATIONS_KEY);

                foreach (String illuminantCondition in result.Split(DELIMITER))
                {
                    availableIlluminantCondition.Add(IlluminantConditionFromString(illuminantCondition));
                }

                return availableIlluminantCondition;
            }
        }

        private IlluminantConditionType IlluminantConditionFromString(String illuminantConditionString)
        {
            if (IlluminantConditionDictionary.ContainsValue(illuminantConditionString))
            {
                return IlluminantConditionDictionary.FirstOrDefault(x => x.Value == illuminantConditionString).Key;
            }

            return IlluminantConditionType.eM0;
        }

        private String IlluminantConditionToString(IlluminantConditionType illuminantCondition)
        {
            if (illuminantConditionDictionary.ContainsKey(illuminantCondition))
            {
                return illuminantConditionDictionary[illuminantCondition];
            }

            return I1_ILLUMINATION_CONDITION_M0;
        }
        public IlluminantConditionType IlluminantType
        {
            get
            {
                string illuminantType = GetOption(I1_RESULT_INDEX_KEY);

                return IlluminantConditionFromString(illuminantType);
            }

            set
            {
                if (!illuminantConditionDictionary.ContainsKey(value))
                {
                    throw new I1Exception(Result.eInvalidArgument, "Unknown measurement mode");
                }

                // WriteLog(LogType.eNormal, "Set measurement mode " + IlluminantConditionDictionary[value]);
                SetOption(I1_RESULT_INDEX_KEY, IlluminantConditionDictionary[value]);
            }
        }

        public List<IlluminantConditionType> AvailableResultIndexKey
        {
            get
            {
                List<IlluminantConditionType> availableResultIndexKey = new List<IlluminantConditionType>();
                String result = GetOption(I1_AVAILABLE_RESULT_INDEXES_KEY);

                foreach (String resultIndex in result.Split(DELIMITER))
                {
                    availableResultIndexKey.Add(IlluminantConditionFromString(resultIndex));
                }

                return availableResultIndexKey;
            }
        }

        //-----------------------------------------
        //-----------------------------------------
        //-----------------------------------------

        public void SetupIlluminant(IlluminantConditionType illuminationMode)
        {
            IlluminantType = illuminationMode;
        }

        private MeasurementModeType measurementModeSelected;
        public void SetupMeasurementMode(MeasurementModeType measurementMode)
        {
            this.measurementModeSelected = measurementMode;
        }

        //-----------------------------------------
        //-----------------------------------------
        //-----------------------------------------

        public bool isCalibrated = false;


        /// <summary>
        /// Calibrate the device
        /// </summary>
        /// <param name="measurementMode">The measurement mode for which the device should be calibrated</param>
        public void Calibrate()
        {
            SetOption(DENSITY_STANDARD_KEY, DENSITY_STANDARD_ANSIT);
            if (!isPro3)
            {
                string aux = GetOption(I1_AVAILABLE_MEASUREMENT_MODES);
                Debug.WriteLine(aux);
                bool checar = false;
                foreach (string mode in aux.Split(DELIMITER))
                {
                    if (mode == "DualReflectanceSpot")
                    {
                        checar = true;
                    }
                }

                if (checar)
                {
                    WriteLog(LogType.eNormal, "Calibrate device");
                    MeasurementMode = MeasurementModeType.eDualReflectanceSpot;

                    Result result = I1_Calibrate(Handle);
                    HandleResult(result);

                    WriteLog(LogType.eNormal, "Device calibrated");
                    isCalibrated = true;
                }
                else
                {
                    WriteLog(LogType.eNormal, "Calibrate device");
                    MeasurementMode = MeasurementModeType.eReflectanceSpot;

                    Result result = I1_Calibrate(Handle);
                    HandleResult(result);

                    WriteLog(LogType.eNormal, "Device calibrated");
                    isCalibrated = true;
                }
            }
            else if (isPro3)
            {
                Result result;
                SetOption(I1PRO3_MEASUREMENT_MODE, I1PRO3_REFLECTANCE_SPOT);
                result = I1PRO3_Calibrate(Handle);
                HandleResult(result);

                WriteLog(LogType.eNormal, "Device calibrated");
                isCalibrated = true;
                WriteLog(LogType.eNormal, "Set measurement mode " + GetOption(I1PRO3_MEASUREMENT_MODE));

            }
        }

        //-----------------------------------------
        //-----------------------------------------
        //-----------------------------------------
        public static int DensitySize { get { return 4; } }

        public List<float[]> listOfDensity;
        public List<float[]> listOfMeasurement;
        public List<List<float[]>> dualScanList;

        public void TriggerMeasurementSpot(MeasurementModeType measurementModeSelected)
        {
            illuminationKey = GetOption(ILLUMINATION_KEY);

            if (!isPro3)
            {
                SetOption(I1_ON_MEASUREMENT_SUCCESS_NO_LED_INDICATION, I1_YES);

                WriteLog(LogType.eNormal, "Trigger measurement");
                MeasurementMode = measurementModeSelected;
                List<IlluminantConditionType> availableIlluminant = AvailableResultIndexKey;
                Result result = I1_TriggerMeasurement(Handle);
                HandleResult(result);
                float[] spectrum = new float[SpectrumSize];
                float[] tristimulus = new float[TristimulusSize];
                float[] density = new float[DensitySize];
                this.tristimulusReq = new List<float[]>();
                float[] tristimulusLab = new float[TristimulusSize];
                this.tristimulusReqLab = new List<float[]>();

                I1_Integer autoDensityIndex = new I1_Integer();
                this.listOfMeasurement = new List<float[]>();
                int illuminant = 0;
                foreach (var placeHolder in availableIlluminant)
                {
                    SetOption(I1_RESULT_INDEX_KEY, IlluminantConditionToString(placeHolder));
                    int aux = I1_GetNumberOfAvailableSamples(Handle);
                    for (int i = 0; i < aux; i++)
                    {
                        result = I1_GetSpectrum(Handle, spectrum, i);

                        // Criar uma cópia do espectro e adicioná-la à lista
                        float[] spectrumCopy = new float[SpectrumSize];
                        float[] spectrumString = new float[SpectrumSize];

                        Array.Copy(spectrum, spectrumCopy, SpectrumSize);
                        for (int j = 0; j < spectrumString.Length; j++)
                        {
                            spectrumString[j] = spectrumCopy[j];

                        }
                        illuminant++;
                        listOfMeasurement.Add(spectrumString);
                    }

                }
                SetOption(COLOR_SPACE_KEY, COLOR_SPACE_RGB);
                result = I1_GetTriStimulus(Handle, tristimulus, 0);

                float[] tristimulusCopy = new float[TristimulusSize];
                Array.Copy(tristimulus, tristimulusCopy, TristimulusSize);
                tristimulusReq.Add(tristimulusCopy);


                result = I1_GetDensities(Handle, density, autoDensityIndex, 0);
                this.listOfDensity = new List<float[]>();
                listOfDensity.Add(density);
                int availableSamples = I1_GetNumberOfAvailableSamples(Handle);
                if(GetOption(I1_HAS_INDICATOR_LED_KEY) == I1_YES)
                {
                    SetOption(I1_INDICATOR_LED_KEY, I1_INDICATOR_LED_MEASUREMENT_SUCCEEDED);
                }
                //SetOption(I1_INDICATOR_LED_KEY, I1_INDICATOR_LED_MEASUREMENT_SUCCEEDED);
                SetOption(COLOR_SPACE_KEY, COLOR_SPACE_CIELab);
                Result resultLab = I1_GetTriStimulus(Handle, tristimulusLab, 0);
                float[] tristimulusCopyLab = new float[TristimulusSize];
                Array.Copy(tristimulusLab, tristimulusCopyLab, TristimulusSize);
                tristimulusReqLab.Add(tristimulusCopyLab);
            }
            else if (isPro3)
            {
                SetOption(I1PRO3_ON_MEASUREMENT_SUCCESS_NO_LED_INDICATION, I1PRO3_YES);

                WriteLog(LogType.eNormal, "Trigger measurement");
                MeasurementMode = measurementModeSelected;
                List<IlluminantConditionType> availableIlluminant = AvailableResultIndexKey;

                Result result = I1PRO3_TriggerMeasurement(Handle);
                HandleResult(result);

                float[] spectrum = new float[SpectrumSize];
                float[] tristimulus = new float[TristimulusSize];
                float[] tristimulusLab = new float[TristimulusSize];
                float[] density = new float[DensitySize];

                this.tristimulusReq = new List<float[]>();
                this.tristimulusReqLab = new List<float[]>();
                this.listOfMeasurement = new List<float[]>();
                this.listOfDensity = new List<float[]>();

                I1_Integer autoDensityIndex = new I1_Integer();
                int illuminant = 0;

                foreach (var placeHolder in availableIlluminant)
                {
                    SetOption(I1PRO3_RESULT_INDEX_KEY, IlluminantConditionToString(placeHolder));
                    int aux = I1PRO3_GetNumberOfAvailableSamples(Handle);

                    for (int i = 0; i < aux; i++)
                    {
                        result = I1PRO3_GetSpectrum(Handle, spectrum, i);
                        float[] spectrumCopy = new float[SpectrumSize];
                        Array.Copy(spectrum, spectrumCopy, SpectrumSize);
                        listOfMeasurement.Add(spectrumCopy);
                        illuminant++;
                    }
                }

                SetOption(COLOR_SPACE_KEY, COLOR_SPACE_RGB);

                // Tristimulus
                result = I1PRO3_GetTriStimulus(Handle, tristimulus, 0);
                float[] tristimulusCopy = new float[TristimulusSize];
                Array.Copy(tristimulus, tristimulusCopy, TristimulusSize);
                tristimulusReq.Add(tristimulusCopy);

                // Density
                result = I1PRO3_GetDensities(Handle, density, autoDensityIndex, 0);
                HandleResult(result);
                float[] densityCopy = new float[DensitySize];
                Array.Copy(density, densityCopy, DensitySize);
                listOfDensity.Add(densityCopy);

                int availableSamples = I1PRO3_GetNumberOfAvailableSamples(Handle);
                SetOption(I1PRO3_INDICATOR_LED_KEY, I1PRO3_INDICATOR_LED_MEASUREMENT_SUCCEEDED);

                SetOption(COLOR_SPACE_KEY, COLOR_SPACE_CIELab);
                Result resultLab = I1PRO3_GetTriStimulus(Handle, tristimulusLab, 0);
                float[] tristimulusCopyLab = new float[TristimulusSize];
                Array.Copy(tristimulusLab, tristimulusCopyLab, TristimulusSize);
                tristimulusReqLab.Add(tristimulusCopyLab);
            }

        }

        public List<float[]> tristimulusReq;
        public List<float[]> tristimulusReqLab;

        public string CurrentMeasurementMode()
        {
            string currentMeasurementMode = GetOption(I1_MEASUREMENT_MODE);
            return currentMeasurementMode;
        }

        public bool isMeasurementUndefined()
        {
            bool isUndefined = false;
            if (GetOption(I1_MEASUREMENT_MODE) == "MeasurementModeUndefined")
            {
                isUndefined = true;
            }
            return isUndefined;
        }
        //-----------------------------------------
        //-----------------------------------------
        //-----------------------------------------

        public void SubstrateConfiguration(float[] substrate)
        {
            if (!isPro3)
            {
                I1_SetSubstrate(Handle, substrate);
            }
            else if (isPro3)
            {
                I1PRO3_SetSubstrate(Handle, substrate);
            }
            SetOption(WHITE_BASE_KEY, WHITE_BASE_PAPER);
        }

        public bool TriggerMeasurementScan(MeasurementModeType measurementModeSelected, int patchNumber)
        {
            Debug.WriteLine(this.illuminantFilter);
            illuminationKey = GetOption(ILLUMINATION_KEY);

            if (!isPro3)
            {
                List<IlluminantConditionType> availableIlluminant = AvailableResultIndexKey;

                //SetOption(ILLUMINATION_KEY, ILLUMINATION_D65);

                SetOption(I1_PATCH_RECOGNITION_KEY, I1_PATCH_RECOGNITION_BASIC);
                SetOption(I1_ON_MEASUREMENT_SUCCESS_NO_LED_INDICATION, I1_YES);

                string sBuffer = patchNumber.ToString();
                Result result = I1_TriggerMeasurement(Handle);
                HandleResult(result);


                float[] spectrum = new float[SpectrumSize];
                I1_Integer autoDensityIndex = new I1_Integer();
                this.listOfDensity = new List<float[]>();

                this.listOfMeasurement = new List<float[]>();

                this.tristimulusReq = new List<float[]>();

                //int illuminant = 0;
                foreach (var placeHolder in availableIlluminant)
                {
                    SetOption(I1_RESULT_INDEX_KEY, IlluminantConditionToString(placeHolder));
                    int aux = I1_GetNumberOfAvailableSamples(Handle);
                    if (patchNumber != aux)
                    {
                        SetOption(I1_INDICATOR_LED_KEY, I1_INDICATOR_LED_MEASUREMENT_FAILED);
                        numberOfPatchesRead = I1_GetNumberOfAvailableSamples(Handle);
                        return false;
                    }

                    for (int i = 0; i < aux; i++)
                    {
                        result = I1_GetSpectrum(Handle, spectrum, i);
                        SetOption(COLOR_SPACE_KEY, COLOR_SPACE_CIELab);

                        // Criar uma cópia do espectro e adicioná-la à lista
                        float[] spectrumCopy = new float[SpectrumSize];
                        Array.Copy(spectrum, spectrumCopy, SpectrumSize);
                        listOfMeasurement.Add(spectrumCopy);

                        float[] tristimulus = new float[TristimulusSize];
                        result = I1_GetTriStimulus(Handle, tristimulus, i);

                        // Criar uma cópia dos tristímulos e adicioná-los à lista
                        float[] tristimulusCopy = new float[TristimulusSize];
                        Array.Copy(tristimulus, tristimulusCopy, TristimulusSize);
                        tristimulusReq.Add(tristimulusCopy);

                        float[] density = new float[DensitySize];
                        result = I1_GetDensities(Handle, density, autoDensityIndex, i);
                        listOfDensity.Add(density);
                    }
                }

                WriteLog(LogType.eNormal, "Number of patches read: " + listOfDensity.Count);

                int availableSamples = I1_GetNumberOfAvailableSamples(Handle);
                if (GetOption(I1_HAS_INDICATOR_LED_KEY) == I1_YES)
                {
                    SetOption(I1_INDICATOR_LED_KEY, I1_INDICATOR_LED_MEASUREMENT_SUCCEEDED);
                }               
            }

            if (isPro3)
            {

                SetOption(I1_ON_MEASUREMENT_SUCCESS_NO_LED_INDICATION, I1_NO);
                List<IlluminantConditionType> availableIlluminant = AvailableResultIndexKey;

                SetOption(I1PRO3_MEASUREMENT_MODE, I1PRO3_REFLECTANCE_SCAN);
                SetOption(I1PRO3_PATCH_RECOGNITION_KEY, I1PRO3_PATCH_RECOGNITION_BASIC);
                SetOption(I1PRO3_ON_MEASUREMENT_SUCCESS_NO_LED_INDICATION, I1PRO3_YES);

                string sBuffer = patchNumber.ToString();
                Result result = I1PRO3_TriggerMeasurement(Handle);
                HandleResult(result);

                float[] spectrum = new float[SpectrumSize];
                I1_Integer autoDensityIndex = new I1_Integer();
                this.listOfDensity = new List<float[]>();
                this.listOfMeasurement = new List<float[]>();
                this.tristimulusReq = new List<float[]>();
                float[] densityArray = new float[4];

                // Handle only the first item from the availableIlluminant list
                if (availableIlluminant.Count > 0)
                {
                    IlluminantConditionType placeHolder = availableIlluminant[0];

                    SetOption(I1PRO3_RESULT_INDEX_KEY, IlluminantConditionToString(placeHolder));
                    int aux = I1PRO3_GetNumberOfAvailableSamples(Handle);
                    if (patchNumber != aux)
                    {
                        numberOfPatchesRead = I1PRO3_GetNumberOfAvailableSamples(Handle);
                        return false;
                    }

                    for (int i = 0; i < aux; i++)
                    {
                        result = I1PRO3_GetSpectrum(Handle, spectrum, i);
                        SetOption(COLOR_SPACE_KEY, COLOR_SPACE_CIELab);

                        float[] spectrumCopy = new float[SpectrumSize];
                        Array.Copy(spectrum, spectrumCopy, SpectrumSize);
                        listOfMeasurement.Add(spectrumCopy);

                        float[] tristimulus = new float[TristimulusSize];
                        result = I1PRO3_GetTriStimulus(Handle, tristimulus, i);

                        float[] tristimulusCopy = new float[TristimulusSize];
                        Array.Copy(tristimulus, tristimulusCopy, TristimulusSize);
                        tristimulusReq.Add(tristimulusCopy);

                        result = I1PRO3_GetDensities(Handle, densityArray, autoDensityIndex, 0);
                        float[] density = new float[4];
                        Array.Copy(densityArray, density, 4);
                        densityArray[4] = 0;
                        listOfDensity.Add(density);
                        Console.WriteLine(listOfDensity);

                    }
                }


                WriteLog(LogType.eNormal, "Number of patches read: " + listOfDensity.Count);

                int availableSamples = I1PRO3_GetNumberOfAvailableSamples(Handle);
                SetOption(I1PRO3_INDICATOR_LED_KEY, I1PRO3_INDICATOR_LED_MEASUREMENT_SUCCEEDED);
                SetOption(I1PRO3_ON_MEASUREMENT_SUCCESS_NO_LED_INDICATION, I1PRO3_NO);
            }

            return true;
        }

        public int numberOfPatchesRead;

        //-----------------------------------------
        //-----------------------------------------
        //-----------------------------------------

        public void SetFilter(string filter)
        {
            illuminantFilter = "e" + filter;
        }

        public string illuminantFilter;
        public int isBackwardsReading = 0;
        public bool readingSuccess = true;
        public bool TriggerMeasurementDualScan()
        {

            List<IlluminantConditionType> availableIlluminant = AvailableResultIndexKey;

            List<float[]> illuminantMatrix = new List<float[]>();

            illuminationKey = GetOption(ILLUMINATION_KEY);

            SetOption(I1_MEASUREMENT_MODE, I1_DUAL_REFLECTANCE_SCAN);

            SetOption(I1_PATCH_RECOGNITION_KEY, I1_PATCH_RECOGNITION_POSITION);

            string sBuffer = numberOfPatches.ToString();

            SetOption(I1_NUMBER_OF_PATCHES_PER_LINE, sBuffer);

            SetOption(I1_SCAN_DIRECTION_KEY, I1_SCAN_DIRECTION_FORWARD);

            SetOption(I1_ON_MEASUREMENT_SUCCESS_NO_LED_INDICATION, I1_YES);

            Result result = I1_TriggerMeasurement(Handle);
            //I1_TriggerMeasurement(Handle);
            HandleResult(result);

            float[] spectrum = new float[SpectrumSize];

            //-----
            SetOption(I1_RESULT_INDEX_KEY, I1_ILLUMINATION_CONDITION_M0);

            if (numberOfPatches != I1_GetNumberOfAvailableSamples(Handle))
            {
                SetOption(I1_INDICATOR_LED_KEY, I1_INDICATOR_LED_MEASUREMENT_FAILED);
                return false;
            }

            string lastScanDirection = GetOption(I1_LAST_SCAN_DIRECTION_KEY);


            if (lastScanDirection == I1_LAST_SCAN_RIGHT_TO_LEFT)
            {
                SetOption(I1_INDICATOR_LED_KEY, I1_INDICATOR_LED_WAIT_FOR_SCAN_RIGHT);

            }
            else if (lastScanDirection == I1_LAST_SCAN_LEFT_TO_RIGHT)
            {
                SetOption(I1_INDICATOR_LED_KEY, I1_INDICATOR_LED_WAIT_FOR_SCAN_LEFT);
            }
            else
            {
                SetOption(I1_HAS_INDICATOR_LED_KEY, I1_INDICATOR_LED_WAIT_FOR_SCAN);
            }

            SetOption(I1_SCAN_DIRECTION_KEY, I1_SCAN_DIRECTION_BACKWARD);
            WaitForButton(Handle);
            I1_TriggerMeasurement(Handle);
            //-----

            if (numberOfPatches != I1_GetNumberOfAvailableSamples(Handle))
            {
                SetOption(I1_INDICATOR_LED_KEY, I1_INDICATOR_LED_MEASUREMENT_FAILED);

                return false;
            }

            SetOption(I1_INDICATOR_LED_KEY, I1_INDICATOR_LED_MEASUREMENT_SUCCEEDED);
            //Result result;
            //I1_Integer autoDensityIndex = new I1_Integer();
            //this.listOfDensity = new List<float[]>();
            this.dualScanList = new List<List<float[]>>();

            foreach (var placeHolder in availableIlluminant)
            {
                SetOption(I1_RESULT_INDEX_KEY, IlluminantConditionToString(placeHolder));
                int aux = I1_GetNumberOfAvailableSamples(Handle);

                illuminantMatrix = new List<float[]>();


                for (int i = 0; i < aux; i++)
                {
                    result = I1_GetSpectrum(Handle, spectrum, i);

                    float[] spectrumCopy = new float[SpectrumSize];
                    //float[] density = new float[DensitySize];

                    Array.Copy(spectrum, spectrumCopy, SpectrumSize);
                    //result = I1_GetDensities(Handle, density, autoDensityIndex, i);
                    //listOfDensity.Add(density);

                    illuminantMatrix.Add(spectrumCopy);
                }
                dualScanList.Add(illuminantMatrix);
            }
            if (HasIndicatorLED)
            {
                SetOption(I1_ON_MEASUREMENT_SUCCESS_NO_LED_INDICATION, I1_YES);
            }
            isMeasuring = false;
            return true;
        }

        public bool isMeasuring = false;

        public void HaveRequestLight(bool isRequest)
        {
            if (isRequest)
            {
                SetOption(I1_ON_MEASUREMENT_SUCCESS_NO_LED_INDICATION, I1_YES);
            }
            else
            {
                SetOption(I1_INDICATOR_LED_KEY, I1_INDICATOR_LED_OFF);

            }
        }

        //-----------------------------------------
        //-----------------------------------------
        //-----------------------------------------

        void WaitForButton(IntPtr handle)
        {
            isMeasuring = true;
            I1_GetButtonStatusD(handle);

            while (I1_GetButtonStatusD(handle) == Result.eButtonNotPressed)
            {
                Thread.Sleep(100);
            }

        }

        //-----------------------------------------
        //-----------------------------------------
        //-----------------------------------------

        /// <summary>
        /// Trigger a measurement
        /// </summary>
        /// <param name="measurementMode">The measurement mode for the measurement</param>
        public void TriggerMeasurement(MeasurementModeType measurementMode)
        {
            WriteLog(LogType.eNormal, "Trigger measurement");

            //MeasurementMode = measurementMode;

            Result result = I1_TriggerMeasurement(Handle);
            HandleResult(result);

            WriteLog(LogType.eNormal, "Measurement done");
        }

        //-----------------------------------------
        //-----------------------------------------
        //-----------------------------------------


        /// <summary>
        /// Get the number of available samples
        /// </summary>
        public int SampleCount
        {
            get
            {
                int availableSampleCount = I1_GetNumberOfAvailableSamples(Handle);
                return availableSampleCount;
            }
        }

        public int IlluminantCount
        {
            get
            {
                int illuminantCount = AvailableResultIndexKey.Count;
                return illuminantCount;
            }
        }

        public bool isM0 = false;
        public bool isM1 = false;
        public bool isM2 = false;

        public int illuminantCount = 0;
        public int AvailableIlluminant()
        {
            if (!isPro3)
            {
                illuminantCount = 0;
                string aux = GetOption(I1_AVAILABLE_ILLUMINATIONS_KEY);
                Console.WriteLine(aux);
                // Verifica se a string contém "M0"
                if (aux.Contains("M0"))
                {
                    isM0 = true;
                    illuminantCount++;
                }
                else
                {
                    isM0 &= false;

                }

                if (aux.Contains("M1"))
                {
                    isM1 = true;
                    illuminantCount++;
                }
                else
                {
                    isM1 &= false;
                }

                if (aux.Contains("M2"))
                {
                    isM2 = true;
                    illuminantCount++;
                }
                else
                {
                    isM2 &= false;
                }
            }
            else
            {
                string aux = GetOption(I1PRO3_AVAILABLE_ILLUMINATIONS_KEY);
                Console.WriteLine(aux);
            }
            return 2;
        }
        /// <summary>
        /// Get the sample at the given index
        /// </summary>
        /// <param name="index">Measurment index</param>
        /// <returns>The spectral data</returns>
        public float[] GetSample(int index)
        {
            float[] spectrum = new float[SpectrumSize];
            Result result = I1_GetSpectrum(Handle, spectrum, index);
            HandleResult(result);
            return spectrum;
        }

        /// <summary>
        /// Get a list of samples of the last measurement
        /// </summary>
        public List<float[]> Samples
        {
            get
            {
                List<float[]> samples = new List<float[]>();
                for (int i = 1; i < IlluminantCount; ++i)
                {
                    samples.Add(GetSample(i));
                }
                return samples;
            }
        }

        //-----------------------------------------
        //-----------------------------------------
        //-----------------------------------------

        /// <summary>
        /// Gets if the device has a UV LED
        /// </summary>
        public bool HasUVLED
        {
            get { return GetOption(I1_HAS_UV_LED_KEY) == I1_YES; }
        }

        /// <summary>
        /// Gets if the device has a UV cut filter
        /// </summary>
        public bool HasUVCutFilter
        {
            get { return GetOption(I1_HAS_UVCUT_FILTER_KEY) == I1_YES; }
        }

        /// <summary>
        /// Gets if the device has a wavelength LED
        /// </summary>
        public bool HasWavelengthLED
        {
            get { return GetOption(I1_HAS_WAVELENGTH_LED_KEY) == I1_YES; }
        }

        /// <summary>
        /// Gets if the device has zebra ruler sensor
        /// </summary>
        public bool HasZebraRulerSensor
        {
            get { return GetOption(I1_HAS_ZEBRA_RULER_SENSOR_KEY) == I1_YES; }
        }

        /// <summary>
        /// Gets if the device has an indicator LED
        /// </summary>
        public bool HasIndicatorLED
        {
            get { return GetOption(I1_HAS_INDICATOR_LED_KEY) == I1_YES; }
        }

        /// <summary>
        /// Gets if the device supports ambient light
        /// </summary>
        public bool SupportsAmbientLight
        {
            get { return GetOption(I1_HAS_AMBIENT_LIGHT_KEY) == I1_YES; }
        }

        /// <summary>
        /// Gets if the device supports low resolution
        /// </summary>
        public bool SupportsLowResolution
        {
            get { return GetOption(I1_HAS_LOW_RESOLUTION_KEY) == I1_YES; }
        }

        /// <summary>
        /// Gets the maximal ruler length [mm]
        /// </summary>
        public int MaximalRulerLength
        {
            get { return Int32.Parse(GetOption(I1_MAX_RULER_LENGTH_KEY)); }
        }

        /// <summary>
        /// Gets if the device is emission only
        /// </summary>
        public bool IsEmissionOnly
        {
            get { return GetOption(I1_IS_EMISSION_ONLY_KEY) == I1_YES; }
        }

        /// <summary>
        /// Gets the hardware revision (A, B, C, ...)
        /// </summary>
        public string HardwareRevision
        {
            get { return GetOption(I1_HW_REVISION_KEY); }
        }

        /// <summary>
        /// Gets the supplier name
        /// </summary>
        public string Supplier
        {
            get { return GetOption(I1_SUPPLIER_NAME_KEY); }
        }

        //-----------------------

        public int numberOfPatches;
        public bool isScanSuccess = false;
        public string illuminationKey;
        public string observerKey;
        public void SetNumberOfPatches(int Patches)
        {
            numberOfPatches = Patches;
        }

        public bool TriggerMeasurementScanHub()
        {
            Debug.WriteLine(this.illuminantFilter);
            if (isPro3)
            {

                isScanSuccess = ScanPro3Trigger();
                GetOption(ILLUMINATION_KEY);

            }
            else
            {
                if (illuminantFilter == "eM0")
                {
                    isScanSuccess = ScanTrigger();
                }
                else if (illuminantFilter == "eM1" || illuminantFilter == "eM2")
                {
                    isScanSuccess = ScanTriggerFiltered();
                }
                else
                {
                    isScanSuccess = TriggerMeasurementDualScan();
                }
            }
            return isScanSuccess;
        }

        //MARCADOR
        public bool ScanPro3Trigger()
        {
            List<IlluminantConditionType> availableIlluminant = AvailableResultIndexKey;

            illuminationKey = GetOption(ILLUMINATION_KEY);
            SetOption(I1PRO3_MEASUREMENT_MODE, I1PRO3_REFLECTANCE_SCAN);
            SetOption(I1PRO3_PATCH_RECOGNITION_KEY, I1PRO3_PATCH_RECOGNITION_BASIC);
            SetOption(I1PRO3_ON_MEASUREMENT_SUCCESS_NO_LED_INDICATION, I1PRO3_YES);

            string sBuffer = numberOfPatches.ToString();
            Result result = I1PRO3_TriggerMeasurement(Handle);
            HandleResult(result);

            float[] spectrum = new float[SpectrumSize];
            I1_Integer autoDensityIndex = new I1_Integer();
            this.listOfDensity = new List<float[]>();
            this.listOfMeasurement = new List<float[]>();
            this.tristimulusReq = new List<float[]>();
            float[] densityArray = new float[4];
            // Handle only the first item from the availableIlluminant list
            if (availableIlluminant.Count > 0)
            {
                IlluminantConditionType placeHolder = availableIlluminant[0];

                SetOption(I1PRO3_RESULT_INDEX_KEY, IlluminantConditionToString(placeHolder));
                int aux = I1PRO3_GetNumberOfAvailableSamples(Handle);
                if (numberOfPatches != aux)
                {
                    numberOfPatchesRead = I1PRO3_GetNumberOfAvailableSamples(Handle);
                    return false;
                }

                if (illuminantFilter == "eM0")
                {
                    SetOption(I1PRO3_RESULT_INDEX_KEY, I1PRO3_ILLUMINATION_CONDITION_M0);

                }
                else if (illuminantFilter == "eM1")
                {
                    SetOption(I1PRO3_RESULT_INDEX_KEY, I1PRO3_ILLUMINATION_CONDITION_M1);

                }
                else if (illuminantFilter == "eM2")
                {
                    SetOption(I1PRO3_RESULT_INDEX_KEY, I1PRO3_ILLUMINATION_CONDITION_M2);

                }



                for (int i = 0; i < aux; i++)
                {
                    result = I1PRO3_GetSpectrum(Handle, spectrum, i);
                    SetOption(COLOR_SPACE_KEY, COLOR_SPACE_CIELab);

                    float[] spectrumCopy = new float[SpectrumSize];
                    Array.Copy(spectrum, spectrumCopy, SpectrumSize);
                    listOfMeasurement.Add(spectrumCopy);

                    float[] tristimulus = new float[TristimulusSize];
                    result = I1PRO3_GetTriStimulus(Handle, tristimulus, i);

                    float[] tristimulusCopy = new float[TristimulusSize];
                    Array.Copy(tristimulus, tristimulusCopy, TristimulusSize);
                    tristimulusReq.Add(tristimulusCopy);


                    result = I1PRO3_GetDensities(Handle, densityArray, autoDensityIndex, i);
                    float[] density = new float[4];
                    Array.Copy(densityArray, density, 4);
                    //densityArray[4] = 0;
                    listOfDensity.Add(density);
                    Console.WriteLine(listOfDensity);
                }

            }

            WriteLog(LogType.eNormal, "Number of patches read: " + listOfDensity.Count);

            int availableSamples = I1PRO3_GetNumberOfAvailableSamples(Handle);
            SetOption(I1PRO3_INDICATOR_LED_KEY, I1PRO3_INDICATOR_LED_MEASUREMENT_SUCCEEDED);
            SetOption(I1PRO3_ON_MEASUREMENT_SUCCESS_NO_LED_INDICATION, I1PRO3_NO);

            return true;
        }

        public bool ScanTrigger()
        {
            List<IlluminantConditionType> availableIlluminant = AvailableResultIndexKey;

            illuminationKey = GetOption(ILLUMINATION_KEY);

            //SetOption(ILLUMINATION_KEY, ILLUMINATION_D65);

            SetOption(I1_PATCH_RECOGNITION_KEY, I1_PATCH_RECOGNITION_BASIC);
            SetOption(I1_ON_MEASUREMENT_SUCCESS_NO_LED_INDICATION, I1_YES);

            string sBuffer = numberOfPatches.ToString();
            Result result = I1_TriggerMeasurement(Handle);
            HandleResult(result);


            float[] spectrum = new float[SpectrumSize];
            I1_Integer autoDensityIndex = new I1_Integer();
            this.listOfDensity = new List<float[]>();

            this.listOfMeasurement = new List<float[]>();

            this.tristimulusReq = new List<float[]>();

            //int illuminant = 0;
            foreach (var placeHolder in availableIlluminant)
            {
                SetOption(I1_RESULT_INDEX_KEY, IlluminantConditionToString(placeHolder));
                int aux = I1_GetNumberOfAvailableSamples(Handle);
                if (numberOfPatches != aux)
                {
                    //SetOption(I1_INDICATOR_LED_KEY, I1_INDICATOR_LED_MEASUREMENT_FAILED);
                    numberOfPatchesRead = I1_GetNumberOfAvailableSamples(Handle);
                    return false;
                }

                for (int i = 0; i < aux; i++)
                {
                    result = I1_GetSpectrum(Handle, spectrum, i);
                    SetOption(COLOR_SPACE_KEY, COLOR_SPACE_CIELab);

                    // Criar uma cópia do espectro e adicioná-la à lista
                    float[] spectrumCopy = new float[SpectrumSize];
                    Array.Copy(spectrum, spectrumCopy, SpectrumSize);
                    listOfMeasurement.Add(spectrumCopy);

                    float[] tristimulus = new float[TristimulusSize];
                    result = I1_GetTriStimulus(Handle, tristimulus, i);

                    // Criar uma cópia dos tristímulos e adicioná-los à lista
                    float[] tristimulusCopy = new float[TristimulusSize];
                    Array.Copy(tristimulus, tristimulusCopy, TristimulusSize);
                    tristimulusReq.Add(tristimulusCopy);

                    float[] density = new float[DensitySize];
                    result = I1_GetDensities(Handle, density, autoDensityIndex, i);
                    listOfDensity.Add(density);
                }
            }

            WriteLog(LogType.eNormal, "Number of patches read: " + listOfDensity.Count);

            int availableSamples = I1_GetNumberOfAvailableSamples(Handle);
            if (GetOption(I1_HAS_INDICATOR_LED_KEY) == I1_YES)
            {
                SetOption(I1_INDICATOR_LED_KEY, I1_INDICATOR_LED_MEASUREMENT_SUCCEEDED);
            }
            //SetOption(I1_INDICATOR_LED_KEY, I1_INDICATOR_LED_MEASUREMENT_SUCCEEDED);
            //SetOption(I1_ON_MEASUREMENT_SUCCESS_NO_LED_INDICATION, I1_NO);
            return true;
        }

        public bool ScanTriggerFiltered()
        {
            I1_SetGlobalOption(I1_RESET, I1_NUMBER_OF_PATCHES_PER_LINE); // reset após falha

            Debug.WriteLine(GetOption(I1_MEASUREMENT_MODE));
            isMeasuring = true;

            List<IlluminantConditionType> availableIlluminant = AvailableResultIndexKey;
            List<float[]> illuminantMatrix = new List<float[]>();

            illuminationKey = GetOption(ILLUMINATION_KEY);

            SetOption(I1_MEASUREMENT_MODE, I1_DUAL_REFLECTANCE_SCAN);
            SetOption(I1_PATCH_RECOGNITION_KEY, I1_PATCH_RECOGNITION_POSITION);
            SetOption(I1_NUMBER_OF_PATCHES_PER_LINE, numberOfPatches.ToString());
            SetOption(I1_SCAN_DIRECTION_KEY, I1_SCAN_DIRECTION_FORWARD);
            SetOption(I1_ON_MEASUREMENT_SUCCESS_NO_LED_INDICATION, I1_YES);

            Result result = I1_TriggerMeasurement(Handle);
            if (result.ToString() != "eNoError")
            {
                SetOption(I1_RESET, I1_ALL); // reset após falha
                isMeasuring = false;
                return false;
            }

            HandleResult(result);

            float[] spectrum = new float[SpectrumSize];
            I1_Integer autoDensityIndex = new I1_Integer();

            //if (numberOfPatches != I1_GetNumberOfAvailableSamples(Handle))
            //{
            //    SetOption(I1_INDICATOR_LED_KEY, I1_INDICATOR_LED_MEASUREMENT_FAILED);
            //    //SetOption(I1_RESET, I1_ALL); // reset após falha
            //    isMeasuring = false;
            //    return false;
            //}

            string lastScanDirection = GetOption(I1_LAST_SCAN_DIRECTION_KEY);
            if (lastScanDirection == I1_LAST_SCAN_RIGHT_TO_LEFT)
                SetOption(I1_INDICATOR_LED_KEY, I1_INDICATOR_LED_WAIT_FOR_SCAN_RIGHT);
            else if (lastScanDirection == I1_LAST_SCAN_LEFT_TO_RIGHT)
                SetOption(I1_INDICATOR_LED_KEY, I1_INDICATOR_LED_WAIT_FOR_SCAN_LEFT);
            else
                SetOption(I1_HAS_INDICATOR_LED_KEY, I1_INDICATOR_LED_WAIT_FOR_SCAN);

            SetOption(I1_SCAN_DIRECTION_KEY, I1_SCAN_DIRECTION_BACKWARD);
            WaitForButton(Handle);

            result = I1_TriggerMeasurement(Handle);
            if (result.ToString() != "eNoError")
            {
                SetOption(I1_RESET, I1_ALL); // reset após falha
                isMeasuring = false;
                return false;
            }

            if (numberOfPatches != I1_GetNumberOfAvailableSamples(Handle))
            {
                SetOption(I1_INDICATOR_LED_KEY, I1_INDICATOR_LED_MEASUREMENT_FAILED);
                SetOption(I1_RESET, I1_ALL); // reset após erro de leitura
                isMeasuring = false;
                return false;
            }

            SetOption(I1_INDICATOR_LED_KEY, I1_INDICATOR_LED_MEASUREMENT_SUCCEEDED);

            dualScanList = new List<List<float[]>>();
            listOfDensity = new List<float[]>();
            listOfMeasurement = new List<float[]>();
            tristimulusReq = new List<float[]>();

            if (illuminantFilter == "eM0")
                SetOption(I1_RESULT_INDEX_KEY, I1_ILLUMINATION_CONDITION_M0);
            else if (illuminantFilter == "eM1")
                SetOption(I1_RESULT_INDEX_KEY, I1_ILLUMINATION_CONDITION_M1);
            else if (illuminantFilter == "eM2")
                SetOption(I1_RESULT_INDEX_KEY, I1_ILLUMINATION_CONDITION_M2);

            foreach (var placeHolder in availableIlluminant)
            {
                SetOption(I1_RESULT_INDEX_KEY, IlluminantConditionToString(placeHolder));
                int aux = I1_GetNumberOfAvailableSamples(Handle);

                if (numberOfPatches != aux)
                {
                    SetOption(I1_INDICATOR_LED_KEY, I1_INDICATOR_LED_MEASUREMENT_FAILED);
                    numberOfPatchesRead = aux;
                    SetOption(I1_RESET, I1_ALL); // reset após erro de consistência de patches
                    isMeasuring = false;
                    return false;
                }

                if (placeHolder.ToString() == illuminantFilter)
                {
                    for (int i = 0; i < aux; i++)
                    {
                        result = I1_GetSpectrum(Handle, spectrum, i);
                        SetOption(COLOR_SPACE_KEY, COLOR_SPACE_CIELab);

                        float[] spectrumCopy = new float[SpectrumSize];
                        Array.Copy(spectrum, spectrumCopy, SpectrumSize);
                        listOfMeasurement.Add(spectrumCopy);

                        float[] tristimulus = new float[TristimulusSize];
                        result = I1_GetTriStimulus(Handle, tristimulus, i);

                        float[] tristimulusCopy = new float[TristimulusSize];
                        Array.Copy(tristimulus, tristimulusCopy, TristimulusSize);
                        tristimulusReq.Add(tristimulusCopy);

                        float[] density = new float[DensitySize];
                        result = I1_GetDensities(Handle, density, autoDensityIndex, i);
                        listOfDensity.Add(density);
                    }
                }
            }

            if (HasIndicatorLED)
                SetOption(I1_ON_MEASUREMENT_SUCCESS_NO_LED_INDICATION, I1_YES);

            return true;
        }

        public void SetStatus(string status)
        {
            switch (status)
            {
                case "ANSIT":
                    SetOption(DENSITY_STANDARD_KEY, DENSITY_STANDARD_ANSIT);
                    break;
                case "DIN":
                    SetOption(DENSITY_STANDARD_KEY, DENSITY_STANDARD_DIN);
                    break;
                case "DINNB":
                    SetOption(DENSITY_STANDARD_KEY, DENSITY_STANDARD_DINNB);
                    break;
                case "ANSIA":
                    SetOption(DENSITY_STANDARD_KEY, DENSITY_STANDARD_ANSIA);
                    break;
                case "ANSIE":
                    SetOption(DENSITY_STANDARD_KEY, DENSITY_STANDARD_ANSIE);
                    break;
                case "ANSII":
                    SetOption(DENSITY_STANDARD_KEY, DENSITY_STANDARD_ANSII);
                    break;
                case "SPI":
                    SetOption(DENSITY_STANDARD_KEY, DENSITY_STANDARD_SPI);
                    break;
                default:
                    SetOption(DENSITY_STANDARD_KEY, DENSITY_STANDARD_ANSIT);
                    break;
            }

        }
        public void SetAbsolutePaper()
        {
            SetOption(WHITE_BASE_KEY, WHITE_BASE_ABSOLUTE);
        }

        //TESTING THIS
        public List<List<float[]>> TriggerMeasurementNew(bool isDensity, bool isLab, bool isSpectrum, string densityKey)
        {
            switch (densityKey)
            {
                case "ANSIA":
                    SetOption(DENSITY_STANDARD_KEY, DENSITY_STANDARD_ANSIA);
                    break;
                case "ANSIE":
                    SetOption(DENSITY_STANDARD_KEY, DENSITY_STANDARD_ANSIE);
                    break;
                case "ANSIT":
                    SetOption(DENSITY_STANDARD_KEY, DENSITY_STANDARD_ANSIT);
                    break;
                case "ANSII":
                    SetOption(DENSITY_STANDARD_KEY, DENSITY_STANDARD_ANSII);
                    break;

            }
            if (!isPro3)
            {
                string k = GetOption(I1_AVAILABLE_MEASUREMENT_MODES);
                string theillu = GetOption(I1_RESULT_INDEX_KEY);

                Debug.WriteLine(k);
                bool checar = false;
                foreach (string mode in k.Split(DELIMITER))
                {
                    if (mode == "DualReflectanceSpot")
                    {
                        checar = true;
                    }
                }

                string densityKeySelected = densityKey;
                if (checar)
                {
                    SetOption(I1_MEASUREMENT_MODE, I1_DUAL_REFLECTANCE_SPOT);
                }
                else
                {
                    SetOption(I1_MEASUREMENT_MODE, I1_REFLECTANCE_SPOT);
                }

                List<IlluminantConditionType> availableIndex = AvailableResultIndexKey;

                Result result = I1_TriggerMeasurement(Handle);
                HandleResult(result);
                float[] tristimulus = new float[5];
                float[] spectrumArray = new float[SpectrumSize + 2];
                List<float[]> densityList = new List<float[]>();
                List<float[]> labList = new List<float[]>();
                List<float[]> spectrumList = new List<float[]>();

                //SetOption(DENSITY_STANDARD_KEY, densityKeySelected);
                float[] densityArray = new float[5];

                I1_Integer autoDensityIndex = new I1_Integer();

                List<List<float[]>> measurementsList = new List<List<float[]>>();

                //---------------------------------
                //00 - density

                //10 - LAB MO
                //11 - LAB M1
                //12 - LAB M2

                //20 - SPECTRUM M0
                //21 - SPECTRUM M1
                //22 - SPECTRUM M2
                //---------------------------------

                //SetOption(COLOR_SPACE_KEY, COLOR_SPACE_CIELab);

                GetOption(I1_AVAILABLE_ILLUMINATIONS_KEY);
                GetOption(I1_AVAILABLE_MEASUREMENT_MODES);
                //GetOption(I1_AVAILABLE_PATCH_RECOGNITIONS_KEY);
                GetOption(I1_AVAILABLE_RESULT_INDEXES_KEY);

                if (isDensity)
                {


                    // SetOption(I1_RESULT_INDEX_KEY, I1_ILLUMINATION_CONDITION_M0);
                    int aux = I1_GetNumberOfAvailableSamples(Handle);


                    result = I1_GetDensities(Handle, densityArray, autoDensityIndex, 0);
                    float[] density = new float[4];
                    Array.Copy(densityArray, density, 4);
                    densityArray[4] = 0;
                    Debug.WriteLine(densityArray[4]);
                    densityList.Add(density);



                    measurementsList.Add(densityList);
                }
                if (isLab)
                {
                    int cont = 01;
                    foreach (var i in availableIndex)
                    {
                        SetOption(I1_RESULT_INDEX_KEY, IlluminantConditionToString(i));
                        int aux = I1_GetNumberOfAvailableSamples(Handle);

                        for (int j = 0; j < aux; j++)
                        {
                            result = I1_GetTriStimulus(Handle, tristimulus, j);
                            float[] labCpy = new float[3];
                            Array.Copy(tristimulus, labCpy, 3);
                            tristimulus[3] = 1;
                            tristimulus[4] = cont;
                            Debug.WriteLine(tristimulus[3] + "" + tristimulus[4]);
                            labList.Add(labCpy);
                        }
                        cont++;
                    }
                    measurementsList.Add(labList);
                }
                if (isSpectrum)
                {
                    int cont = 0;
                    foreach (var i in availableIndex)
                    {
                        SetOption(I1_RESULT_INDEX_KEY, IlluminantConditionToString(i));
                        int aux = I1_GetNumberOfAvailableSamples(Handle);



                        for (int j = 0; j < aux; j++)
                        {
                            result = I1_GetSpectrum(Handle, spectrumArray, j);
                            float[] labCpy = new float[SpectrumSize];
                            Array.Copy(spectrumArray, labCpy, SpectrumSize);
                            spectrumArray[SpectrumSize] = 2;
                            spectrumArray[SpectrumSize + 1] = cont;
                            Debug.WriteLine(spectrumArray[SpectrumSize] + "" + spectrumArray[SpectrumSize + 1]);
                            spectrumList.Add(labCpy);
                        }
                        cont++;
                    }
                    measurementsList.Add(spectrumList);
                }
                return measurementsList;
            }
            else if (isPro3)
            {
                //string k = GetOption(I1PRO3_AVAILABLE_MEASUREMENT_MODES);
                string theillu = GetOption(I1PRO3_RESULT_INDEX_KEY);
                SetOption(I1PRO3_MEASUREMENT_MODE, I1PRO3_REFLECTANCE_SPOT);

                //Debug.WriteLine(k);

                string densityKeySelected = densityKey;

                List<IlluminantConditionType> availableIndex = AvailableResultIndexKey;

                Result result = I1PRO3_TriggerMeasurement(Handle);
                HandleResult(result);
                float[] tristimulus = new float[5];
                float[] spectrumArray = new float[SpectrumSize + 2];
                List<float[]> densityList = new List<float[]>();
                List<float[]> labList = new List<float[]>();
                List<float[]> spectrumList = new List<float[]>();

                //SetOption(DENSITY_STANDARD_KEY, densityKeySelected);
                float[] densityArray = new float[5];

                I1_Integer autoDensityIndex = new I1_Integer();

                List<List<float[]>> measurementsList = new List<List<float[]>>();




                //---------------------------------
                //00 - density

                //10 - LAB MO
                //11 - LAB M1
                //12 - LAB M2

                //20 - SPECTRUM M0
                //21 - SPECTRUM M1
                //22 - SPECTRUM M2
                //---------------------------------

                //SetOption(COLOR_SPACE_KEY, COLOR_SPACE_CIELab);
                if (isDensity)
                {
                    int aux = I1PRO3_GetNumberOfAvailableSamples(Handle);


                    result = I1PRO3_GetDensities(Handle, densityArray, autoDensityIndex, 0);
                    float[] density = new float[4];
                    Array.Copy(densityArray, density, 4);
                    densityArray[4] = 0;
                    Debug.WriteLine(densityArray[4]);
                    densityList.Add(density);



                    measurementsList.Add(densityList);
                }
                if (isLab)
                {
                    int cont = 01;
                    foreach (var i in availableIndex)
                    {
                        SetOption(I1PRO3_RESULT_INDEX_KEY, IlluminantConditionToString(i));
                        int aux = I1PRO3_GetNumberOfAvailableSamples(Handle);

                        for (int j = 0; j < aux; j++)
                        {
                            result = I1PRO3_GetTriStimulus(Handle, tristimulus, j);
                            float[] labCpy = new float[3];
                            Array.Copy(tristimulus, labCpy, 3);
                            tristimulus[3] = 1;
                            tristimulus[4] = cont;
                            Debug.WriteLine(tristimulus[3] + "" + tristimulus[4]);
                            labList.Add(labCpy);
                        }
                        cont++;
                    }
                    measurementsList.Add(labList);
                }
                if (isSpectrum)
                {
                    int cont = 0;
                    foreach (var i in availableIndex)
                    {
                        SetOption(I1PRO3_RESULT_INDEX_KEY, IlluminantConditionToString(i));
                        int aux = I1PRO3_GetNumberOfAvailableSamples(Handle);



                        for (int j = 0; j < aux; j++)
                        {
                            result = I1PRO3_GetSpectrum(Handle, spectrumArray, j);
                            float[] labCpy = new float[SpectrumSize];
                            Array.Copy(spectrumArray, labCpy, SpectrumSize);
                            spectrumArray[SpectrumSize] = 2;
                            spectrumArray[SpectrumSize + 1] = cont;
                            Debug.WriteLine(spectrumArray[SpectrumSize] + "" + spectrumArray[SpectrumSize + 1]);
                            spectrumList.Add(labCpy);
                        }
                        cont++;
                    }
                    measurementsList.Add(spectrumList);
                }

                return measurementsList;
            }
            return null;
        }
        //public bool ScanTriggerFiltered()
        //{
        //    isMeasuring = true;

        //    List<IlluminantConditionType> availableIlluminant = AvailableResultIndexKey;

        //    List<float[]> illuminantMatrix = new List<float[]>();

        //    illuminationKey = GetOption(ILLUMINATION_KEY);

        //    SetOption(I1_MEASUREMENT_MODE, I1_DUAL_REFLECTANCE_SCAN);

        //    SetOption(I1_PATCH_RECOGNITION_KEY, I1_PATCH_RECOGNITION_POSITION);

        //    string sBuffer = numberOfPatches.ToString();

        //    SetOption(I1_NUMBER_OF_PATCHES_PER_LINE, sBuffer);

        //    SetOption(I1_SCAN_DIRECTION_KEY, I1_SCAN_DIRECTION_FORWARD);

        //    SetOption(I1_ON_MEASUREMENT_SUCCESS_NO_LED_INDICATION, I1_YES);

        //    //WaitForButton(Handle);
        //    Result result = I1_TriggerMeasurement(Handle);
        //    if(result.ToString() != "eNoError")
        //    {
        //        isMeasuring = false;
        //    }
        //    //I1_TriggerMeasurement(Handle);
        //    HandleResult(result);

        //    float[] spectrum = new float[SpectrumSize];
        //    I1_Integer autoDensityIndex = new I1_Integer();

        //    int teste = I1_GetNumberOfAvailableSamples(Handle);
        //    //-----

        //    //if (illuminantFilter == "eM0")
        //    //{
        //    //    SetOption(I1_RESULT_INDEX_KEY, I1_ILLUMINATION_CONDITION_M0);
        //    //}
        //    //else if (illuminantFilter == "eM1")
        //    //{
        //    //    SetOption(I1_RESULT_INDEX_KEY, I1_ILLUMINATION_CONDITION_M1);
        //    //}
        //    //else if (illuminantFilter == "eM2")
        //    //{
        //    //    SetOption(I1_RESULT_INDEX_KEY, I1_ILLUMINATION_CONDITION_M2);
        //    //}

        //    //SetOption(I1_RESULT_INDEX_KEY, I1_ILLUMINATION_CONDITION_M0);

        //    if (numberOfPatches != I1_GetNumberOfAvailableSamples(Handle))
        //    {
        //        SetOption(I1_INDICATOR_LED_KEY, I1_INDICATOR_LED_MEASUREMENT_FAILED);
        //        isMeasuring = false;
        //        return false;
        //    }

        //    string lastScanDirection = GetOption(I1_LAST_SCAN_DIRECTION_KEY);


        //    if (lastScanDirection == I1_LAST_SCAN_RIGHT_TO_LEFT)
        //    {
        //        SetOption(I1_INDICATOR_LED_KEY, I1_INDICATOR_LED_WAIT_FOR_SCAN_RIGHT);

        //    }
        //    else if (lastScanDirection == I1_LAST_SCAN_LEFT_TO_RIGHT)
        //    {
        //        SetOption(I1_INDICATOR_LED_KEY, I1_INDICATOR_LED_WAIT_FOR_SCAN_LEFT);
        //    }
        //    else
        //    {
        //        SetOption(I1_HAS_INDICATOR_LED_KEY, I1_INDICATOR_LED_WAIT_FOR_SCAN);
        //    }

        //    SetOption(I1_SCAN_DIRECTION_KEY, I1_SCAN_DIRECTION_BACKWARD);
        //    WaitForButton(Handle);
        //    I1_TriggerMeasurement(Handle);
        //    //-----

        //    if (numberOfPatches != I1_GetNumberOfAvailableSamples(Handle))
        //    {
        //        SetOption(I1_INDICATOR_LED_KEY, I1_INDICATOR_LED_MEASUREMENT_FAILED);
        //        isMeasuring = false;
        //        return false;
        //    }

        //    SetOption(I1_INDICATOR_LED_KEY, I1_INDICATOR_LED_MEASUREMENT_SUCCEEDED);
        //    //Result result;
        //    //I1_Integer autoDensityIndex = new I1_Integer();
        //    //this.listOfDensity = new List<float[]>();
        //    this.dualScanList = new List<List<float[]>>();
        //    this.listOfDensity = new List<float[]>();

        //    this.listOfMeasurement = new List<float[]>();

        //    this.tristimulusReq = new List<float[]>();

        //    if (illuminantFilter == "eM0")
        //    {
        //        SetOption(I1_RESULT_INDEX_KEY, I1_ILLUMINATION_CONDITION_M0);
        //    }
        //    else if (illuminantFilter == "eM1")
        //    {
        //        SetOption(I1_RESULT_INDEX_KEY, I1_ILLUMINATION_CONDITION_M1);
        //    }
        //    else if (illuminantFilter == "eM2")
        //    {
        //        SetOption(I1_RESULT_INDEX_KEY, I1_ILLUMINATION_CONDITION_M2);
        //    }

        //    foreach (var placeHolder in availableIlluminant)
        //    {
        //        SetOption(I1_RESULT_INDEX_KEY, IlluminantConditionToString(placeHolder));
        //        int aux = I1_GetNumberOfAvailableSamples(Handle);
        //        if (numberOfPatches != aux)
        //        {
        //            SetOption(I1_INDICATOR_LED_KEY, I1_INDICATOR_LED_MEASUREMENT_FAILED);
        //            numberOfPatchesRead = I1_GetNumberOfAvailableSamples(Handle);
        //            isMeasuring = false;
        //            return false;
        //        }

        //        if (placeHolder.ToString() == illuminantFilter)
        //        {
        //            for (int i = 0; i < aux; i++)
        //            {
        //                result = I1_GetSpectrum(Handle, spectrum, i);
        //                SetOption(COLOR_SPACE_KEY, COLOR_SPACE_CIELab);

        //                // Criar uma cópia do espectro e adicioná-la à lista
        //                float[] spectrumCopy = new float[SpectrumSize];
        //                Array.Copy(spectrum, spectrumCopy, SpectrumSize);
        //                listOfMeasurement.Add(spectrumCopy);

        //                float[] tristimulus = new float[TristimulusSize];
        //                result = I1_GetTriStimulus(Handle, tristimulus, i);

        //                // Criar uma cópia dos tristímulos e adicioná-los à lista
        //                float[] tristimulusCopy = new float[TristimulusSize];
        //                Array.Copy(tristimulus, tristimulusCopy, TristimulusSize);
        //                tristimulusReq.Add(tristimulusCopy);

        //                float[] density = new float[DensitySize];
        //                result = I1_GetDensities(Handle, density, autoDensityIndex, i);
        //                listOfDensity.Add(density);
        //            }
        //        }
        //    }
        //    if (HasIndicatorLED)
        //    {
        //        SetOption(I1_ON_MEASUREMENT_SUCCESS_NO_LED_INDICATION, I1_YES);
        //    }
        //    return true;
        //}

        public enum MeasurementScanState
        {
            Idle,
            InProgress
        }



        public MeasurementScanState ScanState { get; set; } = MeasurementScanState.Idle;

    }
}
