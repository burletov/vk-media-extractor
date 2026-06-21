using System.Numerics;

namespace MediaExtractorForVK;

internal sealed class HammingBkTree
{
    private Node? _root;

    public void Add(ulong hash, int index)
    {
        if (_root is null)
        {
            _root = new Node(hash, index);
            return;
        }

        var current = _root;
        while (true)
        {
            var distance = Distance(hash, current.Hash);
            if (!current.Children.TryGetValue(distance, out var child))
            {
                current.Children[distance] = new Node(hash, index);
                return;
            }

            current = child;
        }
    }

    public IEnumerable<int> Search(ulong hash, int maxDistance)
    {
        if (_root is null)
        {
            yield break;
        }

        var stack = new Stack<Node>();
        stack.Push(_root);

        while (stack.Count > 0)
        {
            var node = stack.Pop();
            var distance = Distance(hash, node.Hash);
            if (distance <= maxDistance)
            {
                yield return node.Index;
            }

            var minimum = Math.Max(0, distance - maxDistance);
            var maximum = distance + maxDistance;
            foreach (var pair in node.Children)
            {
                if (pair.Key >= minimum && pair.Key <= maximum)
                {
                    stack.Push(pair.Value);
                }
            }
        }
    }

    public static int Distance(ulong left, ulong right) => BitOperations.PopCount(left ^ right);

    private sealed class Node
    {
        public Node(ulong hash, int index)
        {
            Hash = hash;
            Index = index;
        }

        public ulong Hash { get; }
        public int Index { get; }
        public Dictionary<int, Node> Children { get; } = [];
    }
}
