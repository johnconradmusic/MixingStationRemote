using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Speech.Synthesis;
using System.Text;
using System.Threading.Tasks;


namespace MixingStationRemote.Speech;
public class SpeechManager
{
	private static SpeechManager _instance;
	private readonly IAccessibleOutput[] _outputs;

	public static Dictionary<string, string> replacements;

	public static void Init()
	{
		_instance = new SpeechManager();
	}

	public SpeechManager()
	{
		_outputs = new IAccessibleOutput[] { new NvdaOutput(), new SapiOutput() };

		replacements = new();

		replacements.Add("StudioLive", "Studio Live");
	}

	public IAccessibleOutput ScreenReader => GetFirstAvailableOutput();

	private IAccessibleOutput GetFirstAvailableOutput() => _outputs.FirstOrDefault(x => x.IsAvailable());

	public static void Silence()
	{
		_instance.ScreenReader.StopSpeaking();
	}

	public static void Say(object obj, bool interrupt = true)
	{
		if (obj == null || string.IsNullOrEmpty(obj.ToString()))
		{
			return;
		}
		var message = obj.ToString()!;

		foreach (var item in replacements)
		{
			message = message.Replace(item.Key, item.Value);
		}

		Console.WriteLine("Say " + message);

		_instance.ScreenReader.Speak(message, interrupt);
	}
}

public class AutoOutput : IAccessibleOutput
{
	private static readonly IAccessibleOutput[] outputs = { new NvdaOutput(), new SapiOutput() };

	private int rate = 5;

	public AutoOutput()
	{
	}

	public int Rate
	{
		get
		{
			return rate;
		}
		set
		{
			rate = value;
			foreach (var output in outputs)
				if (output is SapiOutput sapi)
					sapi.Rate = value;
		}
	}

	private IAccessibleOutput GetFirstAvailableOutput()
	{
		return outputs.FirstOrDefault(x => x.IsAvailable());
	}

	public bool IsAvailable()
	{
		return outputs.Any(x => x.IsAvailable());
	}

	public void Speak(string text)
	{
		Speak(text, false);
	}

	public void Speak(string text, bool interrupt)
	{
		IAccessibleOutput output = GetFirstAvailableOutput();

		if (interrupt)
		{
			output.StopSpeaking();
		}

		output.Speak(text);
	}

	public void StopSpeaking()
	{
		IAccessibleOutput output = GetFirstAvailableOutput();
		output.StopSpeaking();
	}
}
public interface IAccessibleOutput
{
	int Rate { get; set; }

	bool IsAvailable();

	void Speak(string text);

	void Speak(string text, bool interrupt);

	void StopSpeaking();
}

public class NvdaOutput : IAccessibleOutput
{
	public NvdaOutput()
	{
	}

	public int Rate { get => 1; set => value = 1; }

	public bool IsAvailable()
	{
		if (Environment.Is64BitProcess)
		{
			return NativeMethods64.nvdaController_testIfRunning() == 0;
		}
		else
		{
			return NativeMethods32.nvdaController_testIfRunning() == 0;
		}
	}

	public void Speak(string text)
	{
		Speak(text, false);
	}

	public void Speak(string text, bool interrupt)
	{
		if (interrupt)
		{
			StopSpeaking();
		}

		if (Environment.Is64BitProcess)
		{
			NativeMethods64.nvdaController_speakText(text);
		}
		else
		{
			NativeMethods32.nvdaController_speakText(text);
		}
	}

	public void StopSpeaking()
	{
		if (Environment.Is64BitProcess)
		{
			NativeMethods64.nvdaController_cancelSpeech();
		}
		else
		{
			NativeMethods32.nvdaController_cancelSpeech();
		}
	}

	internal static class NativeMethods32
	{
		[DllImport("./Libraries/nvdaControllerClient32.dll")]
		internal static extern int nvdaController_cancelSpeech();

		[DllImport("./Libraries/nvdaControllerClient32.dll", CharSet = CharSet.Auto)]
		internal static extern int nvdaController_speakText(string text);

		[DllImport("./Libraries/nvdaControllerClient32.dll")]
		internal static extern int nvdaController_testIfRunning();
	}

	internal static class NativeMethods64
	{
		[DllImport("./Libraries/nvdaControllerClient64.dll")]
		internal static extern int nvdaController_cancelSpeech();

		[DllImport("./Libraries/nvdaControllerClient64.dll", CharSet = CharSet.Auto)]
		internal static extern int nvdaController_speakText(string text);

		[DllImport("./Libraries/nvdaControllerClient64.dll")]
		internal static extern int nvdaController_testIfRunning();
	}
}
public class SapiOutput : IAccessibleOutput
{
	private SpeechSynthesizer synth;

	public SapiOutput()
	{
		synth = new SpeechSynthesizer();
		synth.SelectVoiceByHints(VoiceGender.Female);
		synth.Rate = 5;
	}

	public int Rate { get => synth.Rate; set => synth.Rate = value; }

	public bool IsAvailable()
	{
		return true;
	}

	public void Speak(string text)
	{
		Speak(text, false);
	}

	public void Speak(string text, bool interrupt)
	{
		if (interrupt)
		{
			StopSpeaking();
		}

		synth.SpeakAsync(text);
	}

	public void StopSpeaking()
	{
		synth.SpeakAsyncCancelAll();
	}
}