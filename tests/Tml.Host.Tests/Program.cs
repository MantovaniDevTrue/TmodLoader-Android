using System.Text;
using Tml.Mobile.Android;

byte[] Integer(int field, ulong value) => [..Var((ulong)(field << 3)), ..Var(value)];
byte[] Blob(int field, byte[] data) => [..Var((ulong)((field << 3) | 2)), ..Var((ulong)data.Length), ..data];
byte[] Text(int field, string data) => Blob(field, Encoding.UTF8.GetBytes(data));
byte[] Var(ulong value)
{
    var result = new List<byte>();
    do { byte b = (byte)(value & 127); value >>= 7; result.Add(value > 0 ? (byte)(b | 128) : b); } while (value > 0);
    return result.ToArray();
}
string Decode(byte[] data) => NativeCrashTrace.Read(new MemoryStream(data));
void Require(bool success, string name) { if (!success) throw new Exception(name); Console.WriteLine("PASS " + name); }
void Reject(byte[] data, string name)
{
    try { Decode(data); } catch (InvalidDataException) { Console.WriteLine("PASS " + name); return; }
    throw new Exception(name);
}
byte[] Frame(string name) => [..Integer(1, 0xabc), ..Text(4, name), ..Integer(5, 12), ..Text(6, "libmonosgen-2.0.so")];
byte[] Thread(ulong tid, string name) => [..Integer(1, tid), ..Text(2, "SDLThread"), ..Blob(4, Frame(name))];
byte[] Entry(ulong tid, string name) => Blob(16, [..Integer(1, tid), ..Blob(2, Thread(tid, name))]);
byte[] tombstone = [..Integer(6, 77), ..Blob(10, [..Integer(1, 6), ..Text(2, "SIGABRT")]),
    ..Text(14, "assertion failed"), ..Blob(15, Text(1, "test cause")),
    ..Entry(88, "unrelated_thread"), ..Entry(77, "mono_assertion_message"), ..Text(1001, "future field")];
string report = Decode(tombstone);
Require(report.Contains("SIGABRT (6)") && report.Contains("assertion failed") && report.Contains("test cause"), "signal, abort and cause");
Require(report.Contains("Thread: 77 SDLThread") && report.Contains("pc abc") && report.Contains("mono_assertion_message+12"), "crashing thread native backtrace");
Require(!report.Contains("unrelated_thread"), "omit unrelated thread");
Require(Decode([]) == "", "empty trace");
Reject([0x32, 0xff], "truncated length integer");
Reject([0x32, 10, 1], "truncated length field");
Reject([0x80], "truncated tag");
Reject([0], "invalid field number");
Reject([0x0b], "unsupported wire type");
Reject([0x08, ..Enumerable.Repeat((byte)0xff, 10)], "overflow integer");
Require(Decode([0x09, 1,2,3,4,5,6,7,8, 0x15, 1,2,3,4]) == "", "skip fixed fields");
