namespace WrtStoreNs;

public interface IWrtBuffer
{
    string FileName { get; }
    string RootPath { get; }
    void WriteInfo(object obj);
    object ReadInfo();
}
public interface IWrtModel
{
    Guid Id { get; set; }
}
//===================纠结的分隔线==================//
