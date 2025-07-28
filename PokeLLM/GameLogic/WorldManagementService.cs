using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PokeLLM.Game.GameLogic;

public interface IWorldManagementService
{
    //TODO
}

/// <summary>
/// This service contains methods for managing pokemon within the game state
/// </summary>
public class WorldManagementService : IWorldManagementService
{
    private readonly IGameStateRepository _gameStateRepository;
    public WorldManagementService(IGameStateRepository gameStateRepository)
    {
        _gameStateRepository = gameStateRepository;
    }

}
