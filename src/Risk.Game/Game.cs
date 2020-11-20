﻿using System;
using System.Collections.Generic;
using System.Linq;
using Risk.Shared;

namespace Risk.Game
{
    public class Game
    {
        public Game(GameStartOptions startOptions)
        {
            players = startOptions.Players;
            Board = new Board(createTerritories(startOptions.Height, startOptions.Width));
            StartingArmies = startOptions.StartingArmiesPerPlayer;
            gameState = GameState.Initializing;
        }

        private IEnumerable<IPlayer> players;

        public Board Board { get; private set; }
        private GameState gameState { get; set; }

        public DateTime StartTime { get; set; }
        public DateTime EndTime { get; set; }
        public int StartingArmies { get; }
        public GameState GameState => gameState;
        public IEnumerable<IPlayer> Players => players;

        private IEnumerable<Territory> createTerritories(int height, int width)
        {
            var territories = new List<Territory>();
            for (int r = 0; r < height; r++)
            {
                for (int c = 0; c < width; c++)
                {
                    territories.Add(new Territory(new Location(r, c)));
                }
            }
            return territories;
        }

        public void StartJoining()
        {
            gameState = GameState.Joining;
        }

        public void StartGame()
        {
            gameState = GameState.Deploying;
        }

        public bool TryPlaceArmy(string playerToken, Location desiredLocation)
        {
            var placeResult = false;

            if (gameState != Shared.GameState.Deploying)
                return false;

            if (GetPlayerRemainingArmies(playerToken) < 1)
                return false;
            Territory territory;
            try
            {
                territory = Board.GetTerritory(desiredLocation);
            }
            catch { return false; }

            if (territory.Owner == null)
            {
                territory.Owner = GetPlayer(playerToken);
                territory.Armies = 1;
                placeResult = true;
            }
            else if (territory.Owner.Token != playerToken)
            {
                placeResult = false;
            }
            else //owner token == playerToken
            {
                if (GetPlayerRemainingArmies(playerToken) > 0)
                {
                    territory.Armies++;
                    placeResult = true;
                }
                else
                {
                    placeResult = false;
                }
            }

            if (placeResult && CanChangeToAttackState())
                gameState = GameState.Attacking;

            return placeResult;
        }

        public int GetPlayerRemainingArmies(string playerToken)
        {
            var player = GetPlayer(playerToken);
            var armiesOnBoard = GetNumPlacedArmies(player);
            return StartingArmies - armiesOnBoard;
        }

        public IPlayer GetPlayer(string token)
        {
            return players.Single(p => p.Token == token);
        }

        public bool CanChangeToAttackState()
        {
            int totalRemainingArmies = 0;
            foreach (var p in players)
            {
                totalRemainingArmies += GetPlayerRemainingArmies(p.Token);
            }

            return (totalRemainingArmies == 0);
        }

        public bool EnoughArmiesToAttack(Territory attacker)
        {
            return attacker.Armies > 1;
        }

        public bool AttackOwnershipValid(string playerToken, Location from, Location to)
        {
            var territoryFrom = Board.Territories.Single(t => t.Location == from);
            var territoryTo = Board.Territories.Single(t => t.Location == to);
            var player = GetPlayer(playerToken);
            return (territoryFrom.Owner == player && territoryTo.Owner != player);
        }

        public bool PlayerCanAttack(IPlayer player)
        {
            foreach (var territory in Board.Territories.Where(t => t.Owner == player && EnoughArmiesToAttack(t)))
            {
                var neighbors = Board.GetNeighbors(territory);
                return neighbors.Any(n => n.Owner != player);
            }
            return false;
        }

        public GameStatus GetGameStatus()
        {
            IDictionary<string, PlayerArmiesAndTerritories> playerInfo = new Dictionary<string, PlayerArmiesAndTerritories>();

            foreach (var player in Players.ToArray())
            {
                int numPlacedArmies = GetNumPlacedArmies(player);
                int numOwnedTerritories = Board.Territories.Where(t => t.Owner == player)
                                                            .Count();

                var armiesAndTerritories = new PlayerArmiesAndTerritories { NumArmies = numPlacedArmies, NumTerritories = numPlacedArmies };

                playerInfo.Add(player.Name, armiesAndTerritories);
            }

            return new GameStatus(players.Select(p => p.Name), GameState, Board.AsBoardTerritoryList());
        }

        public int GetNumPlacedArmies(IPlayer player)
        {
            return Board.Territories
                        .Where(t => t.Owner == player)
                        .Sum(t => t.Armies);
        }

        public const int MAX_ATTACKER_DICE = 3;
        public const int MAX_DEFENDER_DICE = 2;

        public void SetGameOver()
        {
            gameState = GameState.GameOver;
        }

        public TryAttackResult TryAttack(string attackerToken, Territory attackingTerritory, Territory defendingTerritory, int seed = 0)
        {
            if (canAttack(attackerToken, attackingTerritory, defendingTerritory) is false)
            {
                return new TryAttackResult { AttackInvalid = true };
            }

            var rand = new Random();
            if (seed == 0)
            {
                rand = new Random();
            }
            else
            {
                rand = new Random(seed);
            }

            int[] attackerDice = new int[MAX_ATTACKER_DICE];
            int[] defenderDice = new int[MAX_DEFENDER_DICE];

            for (int i = 0; i < Math.Min(attackingTerritory.Armies, MAX_ATTACKER_DICE); i++)
            {
                attackerDice[i] = rand.Next(1, 7);
            }
            for (int i = 0; i < Math.Min(defendingTerritory.Armies, MAX_DEFENDER_DICE); i++)
            {
                defenderDice[i] = rand.Next(1, 7);
            }
            Array.Sort(attackerDice);
            Array.Sort(defenderDice);
            Array.Reverse(attackerDice);
            Array.Reverse(defenderDice);
            for (int i = 0; i <= defendingTerritory.Armies && i < defenderDice.Length; i++)
            {
                if (attackerDice[i] > defenderDice[i])
                    defendingTerritory.Armies--;
                else
                    attackingTerritory.Armies--;
            }
            if (defendingTerritory.Armies < 1)
            {
                BattleWasWon(attackingTerritory, defendingTerritory);
                return new TryAttackResult {
                    CanContinue = false,
                    AttackInvalid = false
                };
            }
            return new TryAttackResult { CanContinue = attackingTerritory.Armies > 1, AttackInvalid = false };
        }

        private bool canAttack(string attackerToken, Territory attackingTerritory, Territory defendingTerritory)
        {
            return AttackOwnershipValid(attackerToken, attackingTerritory.Location, defendingTerritory.Location)
                 && EnoughArmiesToAttack(attackingTerritory)
                 && Board.AttackTargetLocationIsValid(attackingTerritory.Location, defendingTerritory.Location);
                 //Board.GetNeighbors(attackingTerritory).ToList().Contains(defendingTerritory);
        }

        public int GetNumTerritories(IPlayer player)
        {
            return Board.Territories
                        .Where(t => t.Owner == player)
                        .Count();
        }
        public void BattleWasWon(Territory attackingTerritory, Territory defendingTerritory)
        {
            defendingTerritory.Owner = attackingTerritory.Owner;
            defendingTerritory.Armies = attackingTerritory.Armies - 1;
            attackingTerritory.Armies = attackingTerritory.Armies - defendingTerritory.Armies;
        }
    }
}