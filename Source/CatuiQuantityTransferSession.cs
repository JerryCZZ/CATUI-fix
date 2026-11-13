using System;

public struct CatuiQuantityResult
{
    public int Requested;
    public int Moved;
    public int LeftInSource;
    public bool NotEnoughSpace { get { return Requested > 0 && Moved == 0; } }
    public bool Partial { get { return Moved > 0 && Moved < Requested; } }
    public string Message { get { return NotEnoughSpace ? "Not Enough Space" : null; } }
}



public interface ICatuiQuantityTransferBackend
{



    bool TryTransferPartial(string uniqueToken, int requested,
        out CatuiQuantityResult result, out string error);
}

public sealed class CatuiQuantityTransferSession
{
    public enum Phase { Closed, Editing, AwaitingResult }
    public Phase State { get; private set; }
    public int AvailableAtOpen { get; private set; }
    public int Amount { get; private set; }
    public string Token { get; private set; }
    public string Error { get; private set; }
    public CatuiQuantityResult LastResult { get; private set; }

    public void Open(int stackCount, string uniqueToken)
    {
        if (stackCount < 1 || string.IsNullOrEmpty(uniqueToken))
            throw new ArgumentException("Nonempty stack and unique token required");
        AvailableAtOpen=stackCount;
        Amount=stackCount;
        Token=uniqueToken;
        Error=null;
        LastResult=default(CatuiQuantityResult);
        State=Phase.Editing;
    }

    public void SetAmount(string text)
    {
        RequireEditing();
        Amount=CatuiQuantityTransferRules.NormalizeAmount(text,AvailableAtOpen);
    }
    public void Step(int change)
    {
        RequireEditing();
        Amount=(int)Math.Max(1L,Math.Min((long)AvailableAtOpen,(long)Amount+change));
    }
    public bool Confirm(ICatuiQuantityTransferBackend backend)
    {
        RequireEditing();
        if (backend==null) throw new ArgumentNullException("backend");
        State=Phase.AwaitingResult;
        try
        {
            CatuiQuantityResult result;
            string failure;
            if (!backend.TryTransferPartial(Token,Amount,out result,out failure))
            {
                Error=string.IsNullOrEmpty(failure) ? "Transfer not completed" : failure;

                return false;
            }
            if (result.Requested!=Amount || result.Moved<0 || result.Moved>Amount ||
                result.LeftInSource!=Amount-result.Moved)
            {
                Error="Invalid transfer acknowledgment; refresh inventory";
                return false;
            }
            LastResult=result;
            Error=result.Message;

            Token=null;
            State=Phase.Closed;
            return true;
        }
        catch (Exception)
        {
            Error="Transfer result uncertain; refresh inventory before retrying";
            return false;
        }
    }
    public void Close() { State=Phase.Closed; Token=null; }
    private void RequireEditing()
    {
        if (State!=Phase.Editing) throw new InvalidOperationException("Dialog is not editable");
    }
}
