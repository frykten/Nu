﻿namespace InfinityRpg
open System
open OpenTK
open TiledSharp
open Prime
open Nu

[<AutoOpen>]
module MetamapModule =

    type Direction with

        static member intToDirection n =
            match n with
            | 0 -> North
            | 1 -> East
            | 2 -> South
            | 3 -> West
            | _ -> failwith <| "Invalid Direction conversion from int '" + acstring n + "'."

        static member rand rand =
            let randMax = 3
            let (randValue, rand) = Rand.next2 randMax rand
            let cardinality = Direction.intToDirection randValue
            (cardinality, rand)

        static member walk (source : Vector2I) cardinality =
            match cardinality with
            | North -> Vector2I (source.X, source.Y + 1)
            | East -> Vector2I (source.X + 1, source.Y)
            | South -> Vector2I (source.X, source.Y - 1)
            | West -> Vector2I (source.X - 1, source.Y)

        static member stumble source rand =
            let (cardinality, rand) = Direction.rand rand
            let destination = Direction.walk source cardinality
            (rand, destination)

        static member tryStumbleUntil predicate tryLimit source rand =
            let take = if tryLimit < 0 then id else Seq.take tryLimit
            let stumblings =
                take <|
                    Seq.unfold
                        (fun rand -> Some (Direction.stumble source rand, rand))
                        rand
            Seq.tryFind predicate stumblings

        static member wander stumbleLimit (stumbleBounds : Vector2I) source rand =
            let stumblePredicate =
                fun trail (_, destination : Vector2I) ->
                    destination.X >= 0 &&
                    destination.X <= stumbleBounds.X &&
                    destination.Y >= 0 &&
                    destination.Y <= stumbleBounds.Y &&
                    Set.ofList trail |> Set.contains destination |> not
            Seq.definitize <|
                Seq.unfold
                    (fun ((trail, rand), source) -> Some (Direction.tryStumbleUntil (stumblePredicate trail) stumbleLimit source rand, ((source :: trail, rand), source)))
                    (([], rand), source)

        static member tryWanderUntil predicate tryLimit stumbleLimit stumbleBounds source rand =
            let take = if tryLimit < 0 then id else Seq.take tryLimit
            let wanderings =
                take <|
                    Seq.unfold
                        (fun (source, rand) -> Some (Direction.wander stumbleLimit stumbleBounds source rand, (source, rand)))
                        (source, rand)
            Seq.tryFind predicate wanderings

        static member tryWanderTenToFifteenUnits stumbleBounds source rand =
            let minLength = 10;
            let maxLength = 15;
            let tryLimit = 100;
            let stumbleLimit = 100;
            let predicate = fun (trail : (Rand * Vector2I) seq) ->
                let trail = List.ofSeq <| Seq.take maxLength trail
                if List.length trail >= minLength then
                    let sites = List.map snd trail
                    let uniqueSites = Set.ofList sites
                    List.length sites = Set.count uniqueSites
                else false
            Direction.tryWanderUntil predicate tryLimit stumbleLimit stumbleBounds source rand

        static member tryWanderToDestination stumbleBounds source destination rand =
            let maxLength = 30;
            let tryLimit = 100;
            let stumbleLimit = 100;
            let predicate = fun (trail : (Rand * Vector2I) seq) ->
                let trail = List.ofSeq <| Seq.take maxLength trail
                List.exists (fun point -> snd point = destination) trail
            Direction.tryWanderUntil predicate tryLimit stumbleLimit stumbleBounds source rand

    type MetaTile<'k when 'k : comparison> =
        { ClosedSides : Direction Set
          LockedSides : Map<Direction, 'k>
          Keys : 'k Set }

    type MetaMap<'k when 'k : comparison>  =
        { NavigableSize : Vector2I
          PotentiallyNavigableTiles : Map<Vector2I, 'k MetaTile> }