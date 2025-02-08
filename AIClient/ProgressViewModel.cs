namespace LLMClient;

public class ProgressViewModel : BaseViewModel
{
    private double _progressValue;

    public ProgressViewModel(string message)
    {
        Message = message;
    }

    public double ProgressValue
    {
        get => _progressValue;
        set
        {
            if (value.Equals(_progressValue)) return;
            _progressValue = value;
            OnPropertyChanged();
        }
    }

    public string Message { get; set; }
}