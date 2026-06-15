namespace MediaTidy;

internal sealed class UnionFind
{
    private readonly int[] _parent;
    private readonly byte[] _rank;

    public UnionFind(int size)
    {
        _parent = Enumerable.Range(0, size).ToArray();
        _rank = new byte[size];
    }

    public int Find(int value)
    {
        while (_parent[value] != value)
        {
            _parent[value] = _parent[_parent[value]];
            value = _parent[value];
        }

        return value;
    }

    public void Union(int left, int right)
    {
        var leftRoot = Find(left);
        var rightRoot = Find(right);
        if (leftRoot == rightRoot)
        {
            return;
        }

        if (_rank[leftRoot] < _rank[rightRoot])
        {
            _parent[leftRoot] = rightRoot;
        }
        else if (_rank[leftRoot] > _rank[rightRoot])
        {
            _parent[rightRoot] = leftRoot;
        }
        else
        {
            _parent[rightRoot] = leftRoot;
            _rank[leftRoot]++;
        }
    }
}
