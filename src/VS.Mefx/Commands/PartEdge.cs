namespace VS.Mefx.Commands
{
    /// <summary>
    /// A class to represent a simple edge between nodes.
    /// </summary>
    internal class PartEdge
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="PartEdge"/> class.
        /// </summary>
        /// <param name="other">The Node at the tail of the edge.</param>
        /// <param name="description">The label associated with the node.</param>
        public PartEdge(PartNode other, string description)
        {
            this.Target = other;
            this.Label = description;
        }

        /// <summary>
        /// Gets the node at the tail of the edge.
        /// </summary>
        public PartNode Target { get; private set; }

        /// <summary>
        /// Gets the label associated with the given node.
        /// </summary>
        public string Label { get; private set; }
    }
}
