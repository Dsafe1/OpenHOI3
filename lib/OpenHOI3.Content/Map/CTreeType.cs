namespace OpenHOI3.Content.Map;

/// <summary>
/// One entry of <c>map/trees.txt</c>, a kind of tree the map draws: its name, which terrain.txt's palette entries name in
/// their <c>tree</c>; its sheet of pictures; and its <c>tree_density</c>, the chance a pixel painted with it has a tree - 1
/// or more every pixel (<c>docs/mechanics/mapdraw.md</c>, section 4). Its place in the file is its index.
/// </summary>
public sealed record CTreeType(string Name, string Texture, float Density);
