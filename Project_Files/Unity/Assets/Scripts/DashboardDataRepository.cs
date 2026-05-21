using UnityEngine;
public sealed class DashboardDataRepository
{
    private readonly RakkizSaveRepository _repo;

    public DashboardDataRepository(string fileName)
    {
        _repo = new RakkizSaveRepository(fileName);
    }

    public bool TryLoad(out RakkizSaveData data)
    {
        return _repo.TryLoad(out data);
    }

    public void Save(RakkizSaveData data)
    {
        _repo.Save(data);
    }
}
