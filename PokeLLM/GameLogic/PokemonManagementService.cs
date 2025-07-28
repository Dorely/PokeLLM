using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PokeLLM.Game.GameLogic;

public interface IPokemonManagementService
{
    //TODO
}

/// <summary>
/// This service contains methods for managing pokemon within the game state
/// </summary>
public class PokemonManagementService : IPokemonManagementService
{
    private readonly IGameStateRepository _gameStateRepository;
    public PokemonManagementService(IGameStateRepository gameStateRepository)
    {
        _gameStateRepository = gameStateRepository;
    }

}
