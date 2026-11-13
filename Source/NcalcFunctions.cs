using NCalc;










[XuiBindingNcalcFunction]
public static class CatuiNcalcFunctions
{
















	[XuiBindingNcalcFunction(0, 1, "CATUI_numprefix")]
	public static void CATUI_numprefix(IBindingNcalc _bindingInstance, FunctionArgs _args, object[] _argv)
	{

		string s = _argv[0]?.ToString() ?? "";

		int value = 0;
		int i = 0;
		while (i < s.Length && char.IsDigit(s[i]))
		{
			value = value * 10 + (s[i] - '0');
			i++;
		}

		_args.Result = i > 0 ? value : 0;
	}
}
