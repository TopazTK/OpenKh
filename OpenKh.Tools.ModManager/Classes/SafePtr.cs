using Avalonia.Xaml.Interactions.Custom;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace OpenKh.Tools.ModManager.Classes
{
    public class SafePtr : IDisposable
    {
        protected IntPtr _internalPtr;
        protected int _internalSize;

        public SafePtr(int size)
        {
            _internalSize = size;
            _internalPtr = Marshal.AllocHGlobal(_internalSize);

            if (_internalSize == 0x04)
                Marshal.WriteInt32(_internalPtr, 0x00);

            else if (_internalSize == 0xFF)
                Marshal.Copy(new byte[0xFF], 0, _internalPtr, 0xFF);

            else
                throw new InvalidCastException("Invalid pointer length! Supported values are 0x04 [Int] and 0xFF [String]");
        }

        public object? GetValue()
        {
            if (_internalPtr == IntPtr.Zero)
                return null;

            switch (_internalSize)
            {
                case 0x04:
                    return Marshal.ReadInt32(_internalPtr);

                case 0xFF:
                {
                    var _fetchValue = new byte[0xFF];
                    Marshal.Copy(_internalPtr, _fetchValue, 0, 0xFF);
                    return Encoding.Default.GetString(_fetchValue, 0x00, _fetchValue.IndexOf<byte>(0x00));
                }

                default:
                    return null;
            }
        }

        public bool WriteValue(object value)
        {
            if (_internalPtr == IntPtr.Zero)
                return false;

            switch (_internalSize)
            {
                case 0x04:
                {
                    if (value is int)
                    {
                        Marshal.WriteInt32(_internalPtr, (int)value);
                        return true;
                    }

                    else
                        return false;
                }

                case 0xFF:
                {
                    if (value is string)
                    {
                        var _fetchTextBytes = Encoding.Default.GetBytes((value as string) + "\x00");
                        Marshal.Copy(_fetchTextBytes, 0, _internalPtr, _fetchTextBytes.Length);
                        return true;
                    }

                    else
                        return false;
                }

                default:
                    return false;
            }
        }

        public void Dispose()
        {
            Marshal.FreeHGlobal(_internalPtr);
            _internalPtr = IntPtr.Zero;
        }
    }
}
