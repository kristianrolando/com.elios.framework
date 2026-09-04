using System.Runtime.CompilerServices;

// RunUpdate/RunFixedUpdate/RunLateUpdate are internal because TickDriver is their only caller in
// the game. The test assembly needs the same entry points to step one frame without a player loop.
[assembly: InternalsVisibleTo("Game.Framework.Ticking.Tests")]
