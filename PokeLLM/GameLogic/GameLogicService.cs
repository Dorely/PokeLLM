using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PokeLLM.Game.GameLogic;

public interface IGameLogicService
{
    //TODO
}

/// <summary>
/// This service contains methods for managing pokemon within the game state
/// </summary>
public class GameLogicService : IGameLogicService
{
    private readonly IGameStateRepository _gameStateRepository;
    public GameLogicService(IGameStateRepository gameStateRepository)
    {
        _gameStateRepository = gameStateRepository;
    }

}
