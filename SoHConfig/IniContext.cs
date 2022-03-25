using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SoHConfig
{
    class IniContext
    {
        private string _path;
        private string[] _lines;

        public IniContext(string path)
        {
            _path = path;
            _lines = File.ReadAllLines(path);
        }

        public ControllerBinding? GetBindingForGuidString(string guidString)
        {
            ControllerBinding? binding = null;
            var range = GetControllerBindingRange(guidString);
            if (range.HasValue)
            {
                var (startIndex, endIndex) = range.Value;
                var bindingLines = _lines[(startIndex + 1)..endIndex];
                binding = new ControllerBinding(guidString, bindingLines);
            }
            return binding;
        }

        public string GetGfxBackendValue()
        {
            var index = GetGfxBackendIndex();
            if (index.HasValue)
            {
                var line = _lines[index.Value];
                var value = line.Replace("gfx backend=", "").Trim();
                return value;
            }
            else
            {
                // TODO: Not sure this is possible, soh.exe always
                // generates a backend entry.
                throw new NotImplementedException();
            }
        }

        public void SaveBinding(ControllerBinding binding)
        {
            // Save a backup of the previous config just in case and for bug repros.
            File.WriteAllLines(_path + ".backup", _lines);
            var range = GetControllerBindingRange(binding.GuidString);
            if (range.HasValue)
            {
                var (startIndex, endIndex) = range.Value;
                // TODO: Make less wasteful
                var list = _lines.ToList();
                list.RemoveRange(startIndex, endIndex - startIndex);
                list.InsertRange(startIndex, binding.GenerateConfig());
                _lines = list.ToArray();
                // TODO: Update instead of overwrite
                File.WriteAllLines(_path, _lines);
            }
            else
            {
                // TODO: Make less wasteful
                var list = _lines.ToList();
                list.AddRange(binding.GenerateConfig());
                _lines = list.ToArray();
                // TODO: Update instead of overwrite
                File.WriteAllLines(_path, _lines);
            }
        }

        public void SaveGfxBackend(string value)
        {
            // Save a backup of the previous config just in case and for bug repros.
            File.WriteAllLines(_path + ".backup", _lines);
            var index = GetGfxBackendIndex();
            if (index.HasValue)
            {
                _lines[index.Value] = $"gfx backend={value}";
                // TODO: Update instead of overwrite
                File.WriteAllLines(_path, _lines);
            }
            else
            {
                // TODO: Not sure this is possible, soh.exe always
                // generates a backend entry.
                throw new NotImplementedException();
            }
        }

        private (int, int)? GetControllerBindingRange(string guidString)
        {
            var startIndex = -1;
            var endIndex = -1;
            for (int i = 0; i < _lines.Length; i++)
            {
                var line = _lines[i];
                if (line.StartsWith("[sdl controller binding "))
                {
                    if (startIndex == -1)
                    {
                        var guid = line.Replace("[sdl controller binding ", "").Replace("]", "").Trim();
                        if (guid == guidString)
                        {
                            startIndex = i;
                        }
                    }
                    else
                    {
                        endIndex = i;
                        break;
                    }
                }
            }
            if (startIndex != -1 && endIndex != -1 && startIndex != endIndex)
            {
                return (startIndex, endIndex);
            }
            return null;
        }

        private int? GetGfxBackendIndex()
        {
            var index = -1;
            for (int i = 0; i < _lines.Length; i++)
            {
                var line = _lines[i];
                if (line.StartsWith("gfx backend="))
                {
                    index = i;
                    break;
                }
            }
            if (index < 0)
            {
                return null;
            }
            else
            {
                return index;
            }
        }
    }

    class ControllerBinding
    {
        private string _guid;
        private Dictionary<N64ControllerButton, int> _buttonBindings;
        private Dictionary<ControllerAxisFloat, float> _floatThresholds;
        private Dictionary<ControllerAxisInt, int> _intThresholds;

        public string GuidString => _guid;
        public IReadOnlyDictionary<N64ControllerButton, int> ButtonBindings => _buttonBindings;
        public IReadOnlyDictionary<ControllerAxisFloat, float> FloatThresholdBindings => _floatThresholds;
        public IReadOnlyDictionary<ControllerAxisInt, int> IntThresholdBindings => _intThresholds;

        public ControllerBinding(string guid, string[] lines)
        {
            _guid = guid;
            _buttonBindings = new Dictionary<N64ControllerButton, int>();
            _floatThresholds = new Dictionary<ControllerAxisFloat, float>();
            _intThresholds = new Dictionary<ControllerAxisInt, int>();
            foreach (var line in lines)
            {
                var parts = line.Split('=');
                var buttonKey = parts[0].Trim();
                var buttonValue = parts[1].Trim();
                var button = GetButtonFromString(buttonKey);
                if (button != null)
                {
                    if (int.TryParse(buttonValue, out var buttonBinding))
                    {
                        _buttonBindings.Add(button.Value, buttonBinding);
                    }
                }
                else
                {
                    var axisFloat = GetAxisFloatFromString(buttonKey);
                    if (axisFloat != null)
                    {
                        if (float.TryParse(buttonValue, out var buttonBinding))
                        {
                            _floatThresholds.Add(axisFloat.Value, buttonBinding);
                        }
                    }
                    else
                    {
                        var axisInt = GetAxisIntFromString(buttonKey);
                        if (axisInt != null)
                        {
                            if (int.TryParse(buttonValue, out var buttonBinding))
                            {
                                _intThresholds.Add(axisInt.Value, buttonBinding);
                            }
                        }
                        else
                        {
                            throw new Exception($"Unknown config key \"{buttonKey}\"");
                        }
                    }
                }
            }
            // Fill in all nonparsable values
            var buttons = Enum.GetValues<N64ControllerButton>();
            foreach (var button in buttons)
            {
                if (!_buttonBindings.ContainsKey(button))
                {
                    _buttonBindings.Add(button, GetDefaultValueForButton(button));
                }
            }
            var axisFloats = Enum.GetValues<ControllerAxisFloat>();
            foreach (var axis in axisFloats)
            {
                if (!_floatThresholds.ContainsKey(axis))
                {
                    _floatThresholds.Add(axis, GetDefaultValueForAxisFloat(axis));
                }
            }
            var axisInts = Enum.GetValues<ControllerAxisInt>();
            foreach (var axis in axisInts)
            {
                if (!_intThresholds.ContainsKey(axis))
                {
                    _intThresholds.Add(axis, GetDefaultValueForAxisInt(axis));
                }
            }
        }

        public ControllerBinding(string guid)
        {
            _guid = guid;
            _buttonBindings = new Dictionary<N64ControllerButton, int>();
            _floatThresholds = new Dictionary<ControllerAxisFloat, float>();
            _intThresholds = new Dictionary<ControllerAxisInt, int>();
            ResetToDefault();
        }

        public void SetButtonBinding(N64ControllerButton button, int value)
        {
            if (_buttonBindings.ContainsKey(button))
            {
                _buttonBindings[button] = value;
            }
            else
            {
                _buttonBindings.Add(button, value);
            }
        }

        public void SetAxisFloatBinding(ControllerAxisFloat axis, float value)
        {
            if (_floatThresholds.ContainsKey(axis))
            {
                _floatThresholds[axis] = value;
            }
            else
            {
                _floatThresholds.Add(axis, value);
            }
        }

        public void SetAxisIntBinding(ControllerAxisInt axis, int value)
        {
            if (_intThresholds.ContainsKey(axis))
            {
                _intThresholds[axis] = value;
            }
            else
            {
                _intThresholds.Add(axis, value);
            }
        }

        public void ResetToDefault()
        {
            _buttonBindings.Clear();
            var buttons = Enum.GetValues<N64ControllerButton>();
            foreach (var button in buttons)
            {
                _buttonBindings.Add(button, GetDefaultValueForButton(button));
            }
            _floatThresholds.Clear();
            var axisFloats = Enum.GetValues<ControllerAxisFloat>();
            foreach (var axis in axisFloats)
            {
                _floatThresholds.Add(axis, GetDefaultValueForAxisFloat(axis));
            }
            _intThresholds.Clear();
            var axisInts = Enum.GetValues<ControllerAxisInt>();
            foreach (var axis in axisInts)
            {
                _intThresholds.Add(axis, GetDefaultValueForAxisInt(axis));
            }
        }

        internal IEnumerable<string> GenerateConfig()
        {
            var list = new List<string>();
            list.Add($"[sdl controller binding {_guid}]");
            var buttons = Enum.GetValues<N64ControllerButton>();
            foreach (var button in buttons)
            {
                var value = _buttonBindings[button];
                var key = GetStringFromButton(button);
                list.Add($"{key}={value}");
            }
            var axisFloats = Enum.GetValues<ControllerAxisFloat>();
            foreach (var axis in axisFloats)
            {
                var value = _floatThresholds[axis];
                var key = GetStringFromAxisFloat(axis);
                list.Add($"{key}={value}");
            }
            var axisInts = Enum.GetValues<ControllerAxisInt>();
            foreach (var axis in axisInts)
            {
                var value = _intThresholds[axis];
                var key = GetStringFromAxisInt(axis);
                list.Add($"{key}={value}");
            }
            return list;
        }

        private string GetStringFromButton(N64ControllerButton button)
        {
            switch (button)
            {
                case N64ControllerButton.A:
                    return "btn_a";
                case N64ControllerButton.B:
                    return "btn_b";
                case N64ControllerButton.Start:
                    return "btn_start";
                case N64ControllerButton.CRight:
                    return "btn_cright";
                case N64ControllerButton.CLeft:
                    return "btn_cleft";
                case N64ControllerButton.CDown:
                    return "btn_cdown";
                case N64ControllerButton.CUp:
                    return "btn_cup";
                case N64ControllerButton.R:
                    return "btn_r";
                case N64ControllerButton.L:
                    return "btn_l";
                case N64ControllerButton.DPadRight:
                    return "btn_dright";
                case N64ControllerButton.DPadLeft:
                    return "btn_dleft";
                case N64ControllerButton.DPadDown:
                    return "btn_ddown";
                case N64ControllerButton.DPadUp:
                    return "btn_dup";
                case N64ControllerButton.StickRight:
                    return "btn_stickright";
                case N64ControllerButton.StickLeft:
                    return "btn_stickleft";
                case N64ControllerButton.StickDown:
                    return "btn_stickdown";
                case N64ControllerButton.StickUp:
                    return "btn_stickup";
                case N64ControllerButton.Z:
                    return "btn_z";
                default:
                    throw new ArgumentException();
            }
        }

        private N64ControllerButton? GetButtonFromString(string value)
        {
            switch (value)
            {
                case "btn_a":
                    return N64ControllerButton.A;
                case "btn_b":
                    return N64ControllerButton.B;
                case "btn_start":
                    return N64ControllerButton.Start;
                case "btn_cright":
                    return N64ControllerButton.CRight;
                case "btn_cleft":
                    return N64ControllerButton.CLeft;
                case "btn_cdown":
                    return N64ControllerButton.CDown;
                case "btn_cup":
                    return N64ControllerButton.CUp;
                case "btn_r":
                    return N64ControllerButton.R;
                case "btn_l":
                    return N64ControllerButton.L;
                case "btn_dright":
                    return N64ControllerButton.DPadRight;
                case "btn_dleft":
                    return N64ControllerButton.DPadLeft;
                case "btn_ddown":
                    return N64ControllerButton.DPadDown;
                case "btn_dup":
                    return N64ControllerButton.DPadUp;
                case "btn_stickright":
                    return N64ControllerButton.StickRight;
                case "btn_stickleft":
                    return N64ControllerButton.StickLeft;
                case "btn_stickdown":
                    return N64ControllerButton.StickDown;
                case "btn_stickup":
                    return N64ControllerButton.StickUp;
                case "btn_z":
                    return N64ControllerButton.Z;
                default:
                    return null;
            }
        }

        private int GetDefaultValueForButton(N64ControllerButton button)
        {
            switch (button)
            {
                case N64ControllerButton.CRight:
                    return 514;
                case N64ControllerButton.CLeft:
                    return -514;
                case N64ControllerButton.CDown:
                    return 515;
                case N64ControllerButton.CUp:
                    return -515;
                case N64ControllerButton.R:
                    return 517;
                case N64ControllerButton.L:
                    return 9;
                case N64ControllerButton.DPadRight:
                    return 14;
                case N64ControllerButton.DPadLeft:
                    return 13;
                case N64ControllerButton.DPadDown:
                    return 12;
                case N64ControllerButton.DPadUp:
                    return 11;
                case N64ControllerButton.Start:
                    return 6;
                case N64ControllerButton.Z:
                    return 516;
                case N64ControllerButton.B:
                    return 1;
                case N64ControllerButton.A:
                    return 0;
                case N64ControllerButton.StickRight:
                    return 512;
                case N64ControllerButton.StickLeft:
                    return -512;
                case N64ControllerButton.StickDown:
                    return 513;
                case N64ControllerButton.StickUp:
                    return -513;
                default:
                    throw new ArgumentException();
            }
        }

        private string GetStringFromAxisFloat(ControllerAxisFloat axis)
        {
            switch (axis)
            {
                case ControllerAxisFloat.LeftX:
                    return "sdl_controller_axis_leftx_threshold";
                case ControllerAxisFloat.LeftY:
                    return "sdl_controller_axis_lefty_threshold";
                default:
                    throw new ArgumentException();
            }
        }

        private ControllerAxisFloat? GetAxisFloatFromString(string value)
        {
            switch (value)
            {
                case "sdl_controller_axis_leftx_threshold":
                    return ControllerAxisFloat.LeftX;
                case "sdl_controller_axis_lefty_threshold":
                    return ControllerAxisFloat.LeftY;
                default:
                    return null;
            }
        }

        private float GetDefaultValueForAxisFloat(ControllerAxisFloat axis)
        {
            switch (axis)
            {
                case ControllerAxisFloat.LeftX:
                    return 16.0f;
                case ControllerAxisFloat.LeftY:
                    return 16.0f;
                default:
                    throw new ArgumentException();
            }
        }

        private string GetStringFromAxisInt(ControllerAxisInt axis)
        {
            switch (axis)
            {
                case ControllerAxisInt.RightX:
                    return "sdl_controller_axis_rightx_threshold";
                case ControllerAxisInt.RightY:
                    return "sdl_controller_axis_righty_threshold";
                case ControllerAxisInt.TriggerLeft:
                    return "sdl_controller_axis_triggerleft_threshold";
                case ControllerAxisInt.TriggerRight:
                    return "sdl_controller_axis_triggerright_threshold";
                default:
                    throw new ArgumentException();
            }
        }

        private ControllerAxisInt? GetAxisIntFromString(string value)
        {
            switch (value)
            {
                case "sdl_controller_axis_rightx_threshold":
                    return ControllerAxisInt.RightX;
                case "sdl_controller_axis_righty_threshold":
                    return ControllerAxisInt.RightY;
                case "sdl_controller_axis_triggerleft_threshold":
                    return ControllerAxisInt.TriggerLeft;
                case "sdl_controller_axis_triggerright_threshold":
                    return ControllerAxisInt.TriggerRight;
                default:
                    return null;
            }
        }

        private int GetDefaultValueForAxisInt(ControllerAxisInt axis)
        {
            switch (axis)
            {
                case ControllerAxisInt.RightX:
                    return 16384;
                case ControllerAxisInt.RightY:
                    return 16384;
                case ControllerAxisInt.TriggerLeft:
                    return 7680;
                case ControllerAxisInt.TriggerRight:
                    return 7680;
                default:
                    throw new ArgumentException();
            }
        }
    }

}
