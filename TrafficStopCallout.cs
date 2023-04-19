using CitizenFX.Core;
using CitizenFX.Core.Native;
using FivePD.API;
using FivePD.API.Utils;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TrafficStopCallout
{
    [CalloutProperties("TrafficStop", "HuskyNinja", "v2.0")]
    internal class TrafficStopCallout : Callout
    {
        private static readonly Random _rng = new Random();

        private Ped _player = null;
        private Ped _driver = null;
        private Vehicle _veh = null;

        public TrafficStopCallout()
        {
            _player = Game.PlayerPed;
            PlayerData playerData = Utilities.GetPlayerData();

            InitInfo(_player.Position);
            ResponseCode = 1;
            StartDistance = 50f;
            ShortName = $"Traffic Stop on {World.GetStreetName(_player.Position)}";
            CalloutDescription = $"Officer {playerData.Callsign} is performing a Traffic Stop on {World.GetStreetName(_player.Position)}.";
        }

        //Only start the callout if the player is on a traffic stop
        public override async Task<bool> CheckRequirements()
        {
            await Task.FromResult(0);

            if(Utilities.IsPlayerPerformingTrafficStop())
            {
                return true;
            }
            
            return false;
        }

        public override async Task OnAccept()
        {
            UpdateData();
            InitBlip(25f);

            await Task.FromResult(0);
        }

        public override async void OnStart(Ped Closest)
        {
            base.OnStart(Closest);

            //Ped and Vehicle Data
            _veh = Utilities.GetVehicleFromTrafficStop();
            VehicleData vehData = await _veh.GetData();

            _driver = Utilities.GetDriverFromTrafficStop();
            PedData driverData = await _driver.GetData();

            //Edit the Driver to match the vehicle owner info
            driverData.FirstName = vehData.OwnerFirstName;
            driverData.LastName = vehData.OwnerLastName;

            //Set the data
            _driver.SetData(driverData);

            //Interaction roll determines what happens on the traffic stop
            int interactionRoll = _rng.Next(0, 100);

            //5% chance to have a meele weapon
            if (interactionRoll <= 4)
            {
                //wait for officer to get close
                while (World.GetDistance(Game.PlayerPed.Position, _driver.Position) > 8f) { await BaseScript.Delay(10); }

                //Give Knife
                _driver.Weapons.Give(GetMeeleWeapon(), 1, true, true);

                //Get Out of vehicle
                _driver.Task.LeaveVehicle();

                //Give driver time to leave the vehicle
                await BaseScript.Delay(1500);

                //Attack Officer
                _driver.Task.FightAgainst(Game.PlayerPed);

            }

            //1% chance they fire out of their car while the driver walks up
            if (interactionRoll == 69)
            {
                //wait for officer to get close
                while (World.GetDistance(Game.PlayerPed.Position, _driver.Position) > 5f) { await BaseScript.Delay(10); }

                //Give gun
                _driver.Weapons.Give(WeaponHash.Pistol, 5, true, true);
                _driver.Accuracy = 12;

                //Fire at officer
                _driver.Task.ShootAt(Game.PlayerPed, -1, FiringPattern.SingleShot);

                //Wait a bit to shoot before taking off
                await BaseScript.Delay(_rng.Next(3000, 3500));

                //This ends in a pursuit
                var pursuit = Pursuit.RegisterPursuit(_driver);
                pursuit.Init(true, 35f, 50f, true);
                pursuit.ActivatePursuit();

            }

            //10% chance the driver drives away after the officer gets out of their car
            if (interactionRoll >= 10 && interactionRoll <= 19)
            {
                //wait for officer to get close
                while (World.GetDistance(Game.PlayerPed.Position, _driver.Position) > 5f) { await BaseScript.Delay(10); }

                //Wait for the cop to get close before taking off
                //This ends in a pursuit
                var pursuit = Pursuit.RegisterPursuit(_driver);
                pursuit.Init(true, 35f, 50f, true);
                pursuit.ActivatePursuit();
            }

            //15% chance the driver takes off on foot
            if (interactionRoll >= 30 && interactionRoll <= 44)
            {
                //wait for officer to get close
                while (World.GetDistance(Game.PlayerPed.Position, _driver.Position) > 10f) { await BaseScript.Delay(10); }

                //driver exits vehicle
                _driver.Task.LeaveVehicle();

                //Give time for the driver to exit the vehicle
                await BaseScript.Delay(1500);

                //Wait for the cop to get close before taking off
                //This ends in a pursuit
                var pursuit = Pursuit.RegisterPursuit(_driver);
                pursuit.Init(false, 35f, 50f, true);
                pursuit.ActivatePursuit();
            }

            //15% chance the driver gets out with their hands up
            if (interactionRoll >= 60 && interactionRoll <= 74)
            {
                //Load Animations
                API.RequestAnimDict("missminuteman_1ig_2");
                while (!API.HasAnimDictLoaded("missminuteman_1ig_2")) { await BaseScript.Delay(10); }

                //wait for officer to get close
                while (World.GetDistance(Game.PlayerPed.Position, _driver.Position) > 5f) { await BaseScript.Delay(10); }

                //driver exits vehicle
                _driver.Task.LeaveVehicle();

                //Wait for the driver to exit the vehicle
                await BaseScript.Delay(1500);

                //Hands up
                _driver.Task.PlayAnimation("missminuteman_1ig_2", "handsup_base", 8.0f, -1, AnimationFlags.Loop);

                //Tell AI to get back in car
                Tick += BackInCar;
            }

            //10% chance they just get out of their car and do nothing
            if (interactionRoll >= 20 && interactionRoll <= 29)
            {
                //wait for officer to get close
                while (World.GetDistance(Game.PlayerPed.Position, _driver.Position) > 8f) { await BaseScript.Delay(10); }

                //driver exits vehicle
                _driver.Task.LeaveVehicle();

                //Wait for the driver to exit the vehicle
                await BaseScript.Delay(1500);

                //Make them use their phone
                _driver.Task.StartScenario("WORLD_HUMAN_STAND_MOBILE_UPRIGHT", _driver.Position);

                //Tell AIU to get abck in car
                Tick += BackInCar;
            }
            await Task.FromResult(0);
        }

        public override void OnCancelBefore()
        {
            ShowNetworkedNotification("Remember to ~r~cancel~s~ the ~y~Traffic Stop~s~", "CHAR_CALL911", "CHAR_CALL911", "Dispatch", "Reminder", 1f);
            try
            {
                if(API.DoesEntityExist(_driver.Handle))
                {
                    //Delete all the blips attached to the driver
                    foreach (Blip b in _driver.AttachedBlips)
                    {
                        b.Delete();
                    }
                }
            }
            catch
            {
                //Just some error handling if the deleting blips goes wrong
                //things will carry on as normal
            }

            base.OnCancelBefore();
        }

        //Tick Logic
        public async Task BackInCar()
        {
            if(World.GetDistance(Game.PlayerPed.Position, Utilities.GetDriverFromTrafficStop().Position) >= 3f) { return; }
            Draw3dText("~y~Press~s~ ~b~[H]~s~ to ~g~interact~s~", Utilities.GetDriverFromTrafficStop().Position);

            if(Game.IsControlJustPressed(0, (Control)74))
            {
                Tick -= BackInCar;
                PlayOutOfVehicleDialogue();
            }

            await Task.FromResult(0);
        }

        //Utility functions
        public WeaponHash GetMeeleWeapon()
        {
            List<WeaponHash> weapons = new List<WeaponHash>()
            {
                WeaponHash.Knife,
                WeaponHash.KnuckleDuster,
                WeaponHash.Bat,
                WeaponHash.Wrench,
                WeaponHash.Bottle,
                WeaponHash.Crowbar,
                WeaponHash.GolfClub,
                WeaponHash.PoolCue,
                WeaponHash.StunGun,
            };

            return weapons.SelectRandom();
        }
        private void Draw3dText(string msg, Vector3 pos)
        {
            float textX = 0f, textY = 0f;
            Vector3 camLoc;
            API.World3dToScreen2d(pos.X, pos.Y, pos.Z, ref textX, ref textY);
            camLoc = API.GetGameplayCamCoords();
            float distance = API.GetDistanceBetweenCoords(camLoc.X, camLoc.Y, camLoc.Z, pos.X, pos.Y, pos.Z, true);
            float scale = (1 / distance) * 2;
            float fov = (1 / API.GetGameplayCamFov()) * 100;
            scale = scale * fov * 0.5f;

            API.SetTextScale(0.0f, scale);
            API.SetTextFont(0);
            API.SetTextProportional(true);
            API.SetTextColour(255, 255, 255, 215);
            API.SetTextDropshadow(0, 0, 0, 0, 255);
            API.SetTextEdge(2, 0, 0, 0, 150);
            API.SetTextDropShadow();
            API.SetTextOutline();
            API.SetTextEntry("STRING");
            API.SetTextCentre(true);
            API.AddTextComponentString(msg);
            API.DrawText(textX, textY);
        }
        private async void PlayOutOfVehicleDialogue()
        {
            ShowDialog("~b~Officer~s~: Please return to your vehicle.", 4500, 1f);

            await BaseScript.Delay(4500);

            ShowDialog("~y~Driver~s~: Sorry ~b~Officer~s~.", 4500, 1f);

            Utilities.GetDriverFromTrafficStop().Task.EnterVehicle(Utilities.GetVehicleFromTrafficStop());

            await Task.FromResult(0);
        }
    }
}
