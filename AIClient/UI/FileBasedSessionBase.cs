using System.ComponentModel;
using System.IO;
using LLMClient.Abstraction;

namespace LLMClient.UI;

public abstract class FileBasedSessionBase : NotifyDataErrorInfoViewModelBase, ILLMSession
{
    private DateTime _editTime;

    public DateTime EditTime
    {
        get => _editTime;
        set
        {
            if (value.Equals(_editTime)) return;
            _editTime = value;
            OnPropertyChanged();
        }
    }

    /// <summary>
    /// 用于跟踪对话对象，新实例自动创建id
    /// </summary>
    public string FileName { get; set; } = Guid.NewGuid().ToString();

    /// <summary>
    /// indicate whether data is changed after loading.
    /// </summary>
    public bool IsDataChanged { get; set; } = false;

    private long _tokensConsumption;

    public long TokensConsumption
    {
        get => _tokensConsumption;
        set
        {
            if (value == _tokensConsumption) return;
            _tokensConsumption = value;
            OnPropertyChanged();
        }
    }

    private double _totalPrice;

    public double TotalPrice
    {
        get => _totalPrice;
        set
        {
            if (value.Equals(_totalPrice)) return;
            _totalPrice = value;
            OnPropertyChanged();
        }
    }

    public virtual bool IsBusy
    {
        get { return this._defaultClient.IsResponding; }
    }

    private ILLMClient _defaultClient;

    public ILLMClient DefaultClient
    {
        get => _defaultClient;
        set
        {
            if (Equals(value, _defaultClient)) return;
            if (_defaultClient is INotifyPropertyChanged oldValue)
            {
                oldValue.PropertyChanged -= TagDataChangedOnPropertyChanged;
            }

            if (_defaultClient.Parameters is INotifyPropertyChanged oldParameters)
            {
                oldParameters.PropertyChanged -= TagDataChangedOnPropertyChanged;
            }

            _defaultClient = value;
            OnPropertyChanged();
            TrackClientChanged(value);
        }
    }

    protected FileBasedSessionBase(ILLMClient defaultClient)
    {
        _defaultClient = defaultClient;
        TrackClientChanged(defaultClient);
    }

    private void TrackClientChanged(ILLMClient client)
    {
        if (client is INotifyPropertyChanged newValue)
        {
            newValue.PropertyChanged += TagDataChangedOnPropertyChanged;
        }

        if (_defaultClient.Parameters is INotifyPropertyChanged newParameters)
        {
            newParameters.PropertyChanged += TagDataChangedOnPropertyChanged;
        }
    }

    public void Delete()
    {
        var associateFile = this.GetAssociateFile();
        if (associateFile.Exists)
        {
            associateFile.Delete();
        }
    }

    protected abstract string SaveFolderPath { get; }

    protected FileInfo GetAssociateFile()
    {
        var fullPath = Path.GetFullPath(SaveFolderPath);
        var fileName = Path.GetFullPath(this.FileName + ".json", fullPath);
        return new FileInfo(fileName);
    }

    protected abstract Task SaveToStream(Stream stream);

    public async Task SaveToLocal()
    {
        if (!this.IsDataChanged)
        {
            return;
        }

        var fileInfo = this.GetAssociateFile();
        if (fileInfo.Exists)
        {
            fileInfo.Delete();
        }

        await using (var fileStream = fileInfo.OpenWrite())
        {
            await SaveToStream(fileStream);
        }

        this.IsDataChanged = false;
    }

    private void TagDataChangedOnPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        IsDataChanged = true;
    }
}