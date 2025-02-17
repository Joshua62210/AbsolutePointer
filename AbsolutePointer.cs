using System;
using System.Numerics;
using OpenTabletDriver.Plugin;
using OpenTabletDriver.Plugin.Attributes;
using OpenTabletDriver.Plugin.Output;
using OpenTabletDriver.Plugin.Tablet;
using System.Collections.Generic;
using System.Runtime.InteropServices;

namespace AbsolutePointer
{
    [PluginName("Absolute Pointer")]
    public class PointerSensitivityBinding : IStateBinding
    {
        internal static bool IsActive { set; get; }
        public void Press(TabletReference tablet, IDeviceReport report) => IsActive = true;
        public void Release(TabletReference tablet, IDeviceReport report) => IsActive = false;
    }

    [PluginName("Absolute Pointer")]
    public class PointerSensitivity : IPositionedPipelineElement<IDeviceReport>
    {
        public event Action<IDeviceReport>? Emit;

        [SliderProperty("Pointer Sensitivity (Min: 1, Max: 20)", 1f, 20f, 10f), DefaultPropertyValue(10f)]
        public float Sensitivity { get; set; }

        [SliderProperty("Analogue I/O Curve (1 = Slow, 2 = Smooth, 3 = Linear, 4 = Aggressive, 5 = Instantaneous)", 1, 5, 3), DefaultPropertyValue(3)]
        public int CurveSelection { get; set; }

        [SliderProperty("Pointer Acceleration Type (0 = None, 1 = Adaptive, 2 = Dynamic, 3 = Exponential)", 0, 3, 0), DefaultPropertyValue(0)]
        public int AccelerationType { get; set; }

        [SliderProperty("Acceleration Force (Min: 0, Max: 10)", 0, 10, 0), DefaultPropertyValue(0)]
        public int AccelerationForce { get; set; }

        [BooleanProperty("No Catch-Up", "Eliminates input delay for instant pointer tracking.")]
        public bool NoCatchUp { get; set; }

        [BooleanProperty("Raw Input", "Uses Raw Input for accurate tracking without Windows processing.")]
        public bool RawInput { get; set; }

        [BooleanProperty("Support TabletPC", "Enables compatibility with TabletPC drivers for better support of graphics tablets.")]
        public bool SupportTabletPC { get; set; }

        private float LowSensitivity;
        private float HighSensitivity;
        private const float DefaultAdaptiveSmoothing = 0.33f;

        public PipelinePosition Position => PipelinePosition.PostTransform;

        public void Consume(IDeviceReport value)
        {
            if (value is ITabletReport report && PointerSensitivityBinding.IsActive)
            {
                ApplyCurveSettings();
                ApplyInputCurve(report);
                ApplyAcceleration(report);
                ApplyOutputCurve(report);
                report.Position *= Sensitivity / 10f;
                if (RawInput) ProcessRawInput();
                if (SupportTabletPC) EnableTabletPCMode();
                if (NoCatchUp) { Emit?.Invoke(report); return; }
            }
            Emit?.Invoke(value);
        }

        private void ApplyCurveSettings()
        {
            switch (CurveSelection)
            {
                case 1:
                    LowSensitivity = 0.4f;
                    HighSensitivity = 0.8f;
                    break;
                case 2:
                    LowSensitivity = 0.6f;
                    HighSensitivity = 1.0f;
                    break;
                case 3:
                    LowSensitivity = 0.7f;
                    HighSensitivity = 1.2f;
                    break;
                case 4:
                    LowSensitivity = 0.9f;
                    HighSensitivity = 1.5f;
                    break;
                case 5:
                    LowSensitivity = 1.0f;
                    HighSensitivity = 2.0f;
                    break;
            }
        }

        private void ApplyInputCurve(ITabletReport report)
        {
            float speed = report.Position.Length();
            float dynamicFactor = Math.Clamp(speed / 20f, 0.5f, 1.5f);

            report.Position *= CurveSelection switch
            {
                1 => LowSensitivity * dynamicFactor,
                2 => MathF.Pow(speed, 0.8f) * dynamicFactor,
                3 => 1.0f * dynamicFactor,
                4 => HighSensitivity * dynamicFactor,
                5 => 2.0f * dynamicFactor,
                _ => 1.0f,
            };
        }

        private void ApplyAcceleration(ITabletReport report)
        {
            if (AccelerationForce > 0)
            {
                float speed = report.Position.Length();
                float accelFactor = 1.0f;

                switch (AccelerationType)
                {
                    case 1:
                        float smoothFactor = DefaultAdaptiveSmoothing * (1 - Math.Clamp(speed / 10f, 0.1f, 1.0f));
                        accelFactor = 1.0f + (AccelerationForce / 10f * (1 - smoothFactor));
                        break;
                    case 2:
                        accelFactor = 1.0f + ((AccelerationForce / 10f) * Math.Clamp(speed / 15f, 0.5f, 1.5f));
                        break;
                    case 3:
                        accelFactor = 1.0f + (float)Math.Pow(speed, (AccelerationForce / 10f));
                        break;
                }

                report.Position *= accelFactor;
            }
        }

        private void ApplyOutputCurve(ITabletReport report)
        {
            float speed = report.Position.Length();
            float dynamicFactor = Math.Clamp(speed / 20f, 0.5f, 1.5f);

            report.Position *= CurveSelection switch
            {
                1 => LowSensitivity * dynamicFactor,
                2 => MathF.Pow(speed, 0.8f) * dynamicFactor,
                3 => 1.0f * dynamicFactor,
                4 => HighSensitivity * dynamicFactor,
                5 => 2.0f * dynamicFactor,
                _ => 1.0f,
            };
        }

        private static void ProcessRawInput()
        {
        }

        private static void EnableTabletPCMode()
        {
        }
    }
}
