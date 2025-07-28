using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PokeLLM.Game.GameLogic;

public interface INpcManagementService
{
    //TODO
}

/// <summary>
/// This service contains methods for managing pokemon within the game state
/// </summary>
public class NpcManagementService : INpcManagementService
{
    private readonly IGameStateRepository _gameStateRepository;
    public NpcManagementService(IGameStateRepository gameStateRepository)
    {
        _gameStateRepository = gameStateRepository;
    }

}
