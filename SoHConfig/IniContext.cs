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
                            startIndex = i + 1;
                        }
                    }
                    else
                    {
                        endIndex = i;
                    }
                }
            }
            if (startIndex != -1 && endIndex != -1 && startIndex != endIndex)
            {
                var bindingLines = _lines[startIndex..endIndex];
                binding = new ControllerBinding(guidString, bindingLines);
            }
            return binding;
        }
    }

    class ControllerBinding
    {
        private string _guid;
        private Dictionary<N64ControllerButton, int> _buttonBindings;

        public ControllerBinding(string guid, string[] lines)
        {
            _guid = guid;
            _buttonBindings = new Dictionary<N64ControllerButton, int>();
            foreach (var line in lines)
            {
                Debug.WriteLine(line);
            }
        }

        public ControllerBinding(string guid)
        {
            _guid = guid;
            _buttonBindings = new Dictionary<N64ControllerButton, int>();
            // TODO: Fill in default bindings
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
    }

}
