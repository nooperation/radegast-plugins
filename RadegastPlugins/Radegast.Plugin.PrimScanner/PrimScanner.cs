using System.Threading;
using OpenMetaverse;
using Radegast;
using System;
using System.Collections.Generic;
using System.Text;
using Radegast.Rendering;

namespace PrimScanner
{
	[Radegast.Plugin(Name = "PrimScanner Plugin", Description = "PrimScanner Plugin.", Version = "1.0")]
	public class PrimScanner : IRadegastPlugin
	{
		//Timer clearPreviousRequestsTimer = null;

		private RadegastInstance instance;
		private Dictionary<UUID, uint> requestedUpdates = new Dictionary<UUID, uint>();

		private List<UUID> privateMessageTargets = new List<UUID>()
		{
			new UUID("24036859-e20e-40c4-8088-be6b934c3891"), // Kyomuno Tsuki
		};

		private List<UUID> groupsToMessage = new List<UUID>()
		{
			new UUID("79f9ab8a-7de2-bb83-06a1-e1b119307fb0"), // Badcoders
		};

		private HashSet<UUID> ignoredOwners = new HashSet<UUID>()
		{
			new UUID("7bfccbb7-91b4-43fe-89b9-956e5821338e"),
			new UUID("1b73efe6-0dfe-4fae-b342-efc14d94685a"),
			new UUID("4a56dac0-a1ca-47ff-8852-5714d79bd73b"),
			new UUID("2ffa34fe-f6bb-415e-bf4d-6fd41307e4ba"),
			new UUID("d08b4332-fdde-4a36-ac06-3e6c9a38542e"),
			new UUID("a0a8a7bd-5e2b-44b1-ad03-c60f31ec33a3"),
			new UUID("e57b21e7-0a52-455a-aab9-4573e9922a6d"), // Fureh Rosewood
			new UUID("dee1223d-ffc7-4fba-be15-9e2141705314"), // Eo Fenstalker

			new UUID("6b1067d1-1bdd-4e8e-8e07-8427b1ed29c0"), // ScentualLust
			new UUID("3857ee5c-4081-4533-aec3-2bb5f05d1ef5"), // Dash (shurikenangel)
			new UUID("156da2c7-c246-4542-ba52-e2dd4e3e6734"), // Pinkie Pie (nicelus.borrelly)
			new UUID("8a2ef4b0-6a31-4b9a-9aeb-7270f5640732"), // OldVamp
			new UUID("b1896295-b70a-476d-bbf5-535a9a810e27"), // Amehana Arashi

			
		};

		private HashSet<UUID> ignoredObjects = new HashSet<UUID>()
		{
			// LUSKWOOD:
			new UUID("f4569719-52ae-1913-7125-6c58474d55b8"), // Owner: Pomke Nohkan | Name: Object
			new UUID("6d9acc47-ab5e-7be3-109a-9e8a738af501"), // Owner: Dougal Jacobs | Name: Platform Tree
			new UUID("bae1fedd-2a75-9965-07e7-2b4b52ec82f7"), // Owner: Pomke Nohkan | Name: Relic Tree 5 - Part 2
			new UUID("9a115949-830e-ac6b-717d-169694d45679"), // Owner: Pomke Nohkan | Name: Relic Tree 5 - Part 1
			new UUID("599e49db-d56f-3170-edb6-866a7a5bcb21"), // Owner: Pomke Nohkan | Name: Relic Tree 4 - Part 2
			new UUID("37414cf8-683c-dcea-893d-1c3625131fb7"), // Owner: Pomke Nohkan | Name: Relic Tree 3 - Part 3
			new UUID("db072aa1-ecc2-2896-0190-fd67cff46544"), // Owner: Pomke Nohkan | Name: Relic Tree 3 - Part 3
			new UUID("ff8601a0-8c13-bce1-6727-a8edc398fa00"), // Owner: Pomke Nohkan | Name: Relic Tree 3 - Part 3
			new UUID("d77cabe6-a433-c264-fd05-04a921bdc6f5"), // Owner: Pomke Nohkan | Name: Relic Tree 2 - Part 3
			new UUID("c31bc64e-6828-25d3-f624-48f23cc3ec4b"), // Owner: Pomke Nohkan | Name: Relic Tree 4 - Part 2
			new UUID("0eb44279-42fe-c2af-33e6-a9f3158009d4"), // Owner: Pomke Nohkan | Name: Relic Tree 4 - Part 2
			new UUID("29dc5357-a663-4874-5402-a2bab81debbb"), // Owner: Pomke Nohkan | Name: Relic Tree 4 - Part 1
			new UUID("058f58a3-6d8a-d161-d0a4-99c4228d0328"), // Owner: Pomke Nohkan | Name: Relic Tree 5 - Part 2
			new UUID("bcf68ebf-fba8-acd2-e1aa-08752111642c"), // Owner: Pomke Nohkan | Name: Relic Tree 3 - Part 1
			new UUID("30cd8856-4fde-1c57-89cb-c45fa2922758"), // Owner: Pomke Nohkan | Name: Relic Tree 2 - Part 3
			new UUID("f3cca303-d174-5aff-1f7a-8ada0114c655"), // Owner: Pomke Nohkan | Name: Relic Tree 2 - Part 3
			new UUID("24f3453e-db51-0a31-69e6-4537df29bf94"), // Owner: Dougal Jacobs | Name: 5 x 5 x 80
			new UUID("b26a71c6-8ec1-66fd-4c20-23ec26ff76a9"), // Owner: Pomke Nohkan | Name: Relic Tree 5 - Part 1
			new UUID("38731d09-7ad0-27ee-e66d-3d5bfd07791a"), // Owner: Pomke Nohkan | Name: Relic Tree 2 - Part 3
			new UUID("63a4a119-415b-19fe-59e4-ada33e2ce8cc"), // Owner: Pomke Nohkan | Name: Relic Tree 5 - Part 1
			new UUID("80838ee6-049d-4b5a-ba3c-83f8c1fe4a6a"), // Owner: Pomke Nohkan | Name: Relic Tree 2 - Part 3
			new UUID("2822c41a-bf11-7b1a-1be4-b93b1e3edc90"), // Owner: Dougal Jacobs | Name: 10 x 10 x 100
			new UUID("f8e20232-6c74-458a-7406-63653655baa3"), // Owner: Pomke Nohkan | Name: Relic Tree 3 - Part 3
			new UUID("09a3b739-08a3-ac64-98f4-841c4c2947a6"), // Owner: Pomke Nohkan | Name: Relic Tree 4 - Part 2
			new UUID("6a9369b7-75e0-2623-a136-7986f59a072a"), // Owner: Pomke Nohkan | Name: Relic Tree 5 - Part 2
			new UUID("20fc9145-ecc1-31f9-247d-38ebb12c15b3"), // Owner: Pomke Nohkan | Name: Relic Tree 4 - Part 2
			new UUID("8d31a818-2b20-ddb0-ef87-cc0a0dfc42f3"), // Owner: Dougal Jacobs | Name: 10 x 10 x 100
			new UUID("3051fc75-dd93-c595-00c9-f968fda2c644"), // Owner: Pomke Nohkan | Name: Relic Tree 4 - Part 2
			new UUID("57379549-ed87-67f9-ba0d-8cc4718c70a5"), // Owner: Pomke Nohkan | Name: Relic Tree 4 - Part 2
			new UUID("6729fbbb-600f-27ef-2ad6-091dcc027dd4"), // Owner: Pomke Nohkan | Name: Relic Tree 2 - Part 3
			new UUID("2c187f64-6a0e-3be1-1c73-1462f7be9055"), // Owner: Dougal Jacobs | Name: 10 x 10 x 100
			new UUID("75e96333-8f78-4f09-20cb-10cba047985a"), // Owner: Dougal Jacobs | Name: Megatree
			new UUID("4ead79a9-4d5f-0f6f-213b-85885eba6985"), // Owner: Dougal Jacobs | Name: 10 x 10 x 100
			new UUID("827ee08e-b75f-8465-1a62-ee49c014e190"), // Owner: Dougal Jacobs | Name: 10 x 10 x 100
			new UUID("02afca79-2e46-9c23-98f5-1635259278a6"), // Owner: Pomke Nohkan | Name: Relic Tree 5 - Part 2
			new UUID("23b22f26-3113-0237-544e-4e4a638c0198"), // Owner: Pomke Nohkan | Name: Relic Tree 4 - Part 2
			new UUID("f4cb2364-6142-7a23-e447-2667aa0cf3ff"), // Owner: Dougal Jacobs | Name: MegaTree
			new UUID("ccebef0b-0a60-463d-78bc-816476f748dc"), // Owner: Dougal Jacobs | Name: 96.5 172 28.75 Tree Trunk
			new UUID("49cab189-97d1-e75c-e7b2-bddf64d49acc"), // Owner: Pomke Nohkan | Name: Relic Tree 5 - Part 2
			new UUID("f8abb5d1-7f45-0394-4479-04fffa0bf448"), // Owner: Pomke Nohkan | Name: Relic Tree 5 - Part 1
			new UUID("e36be6f7-f8a9-bae6-4e7d-aa949cffec8d"), // Owner: Pomke Nohkan | Name: Relic Tree 4 - Part 1
			new UUID("4183fc37-f11e-8b88-352b-74680409fefa"), // Owner: Pomke Nohkan | Name: Level 1 Pod A
			new UUID("30278ec2-caa1-b0dd-a876-bdca6ab52908"), // Owner: Pomke Nohkan | Name: Relic Tree 4 - Part 1
			new UUID("9e2d573d-b560-e482-b9a8-c42acfb5bcae"), // Owner: Pomke Nohkan | Name: Level 1 Pod D
			new UUID("dc9abe27-06d7-47d5-7d1b-83aa369df426"), // Owner: Pomke Nohkan | Name: Level 1 Pod B
			new UUID("2c8ec360-c3fd-45cd-78f2-84a716d7d5b0"), // Owner: Pomke Nohkan | Name: Relic Tree 4 - Part 1
			new UUID("47cff91b-e044-0700-7de0-596cb4ebc4b5"), // Owner: Pomke Nohkan | Name: Relic Tree 2 - Part 1
			new UUID("380e8386-e7e3-f06e-b691-09405c5c37d3"), // Owner: Pomke Nohkan | Name: Level 1 Pod C
			new UUID("ef7e860a-1548-62a6-ed5c-16b0588a7305"), // Owner: Pomke Nohkan | Name: Relic Tree 5 - Part 2
			new UUID("2e046fe6-5c19-37b2-8ff1-36d5ecea9a8b"), // Owner: Pomke Nohkan | Name: Relic Tree 5 - Part 1
			new UUID("18bb80d6-e316-efe0-9294-2ffde9f448f9"), // Owner: Pomke Nohkan | Name: Relic Tree 2 - Part 1
			new UUID("0a01e98a-81a5-b1f8-0e05-02a8dd58454c"), // Owner: Pomke Nohkan | Name: Spinny Ring
			new UUID("5f155afe-223f-d851-cb57-b0cb5f81b8b2"), // Owner: Pomke Nohkan | Name: Relic Tree 4 - Part 1
			new UUID("bc91d878-f262-239b-3197-859c6eaf918b"), // Owner: Pomke Nohkan | Name: Relic Tree 5 - Part 1
			new UUID("2fbbb82c-7c85-7ec8-0ac9-097284c88432"), // Owner: Pomke Nohkan | Name: Relic Tree 3 - Part 1
			new UUID("47ca3758-4bfb-bac5-5685-c5d41dfa394d"), // Owner: Pomke Nohkan | Name: Relic Tree 4 - Part 1
			new UUID("3bd917fb-ed9f-311c-a831-367ce92d224f"), // Owner: Pomke Nohkan | Name: Relic Tree 5 - Part 1
			new UUID("1f6cb503-ad9f-2583-a4f9-5eccad0392b5"), // Owner: Kumba Digfoot | Name: Fabbrica 1
			new UUID("0ff903f6-df98-07c0-42a4-1e0b18300b31"), // Owner: Kumba Digfoot | Name: Object
			new UUID("de23ee39-28d3-29d4-b5e0-5ab7842a6c94"), // Owner: Kumba Digfoot | Name: inVerse - sky platform screen n.1  screen
			new UUID("46774d21-d619-6a30-997b-53f8a3b06096"), // Owner: Kumba Digfoot | Name: Object
			new UUID("557b7c2c-ce3a-4c45-37e9-65ead16d5eaf"), // Owner: Kumba Digfoot | Name: Object
			new UUID("6057eea2-043b-4a8d-2cf0-ba722ea24b78"), // Owner: Kumba Digfoot | Name: Object
			new UUID("7610537a-0312-8155-804f-4c882d73ed89"), // Owner: Tatsuta Resident | Name: Object
			new UUID("faaa2b94-6a24-6884-483d-f7c42ee40d7c"), // Owner: Kamatz Kuhr | Name: Object
			new UUID("c05b14ad-dfdd-7270-7b67-53743c290910"), // Owner: Codex Rau | Name: Luskwood Amphitheater
			new UUID("10babf55-a468-ec3d-4649-28e676e98101"), // Owner: Tatsuta Resident | Name: Forest screen 64x32x0.1-unvisible outside-phantom
			new UUID("a1b18511-28f9-0980-7601-4d8403e1897b"), // Owner: Tatsuta Resident | Name: Object
			new UUID("cb02b01d-4e15-3a59-930f-929814fb172d"), // Owner: Tatsuta Resident | Name: Forest screen 56x32x0.1-unvisible outside-phantom
			new UUID("dbd9b1fc-566a-fccd-6545-48c663d728ad"), // Owner: Tatsuta Resident | Name: Forest screen 64x32x0.1-unvisible outside-phantom
			new UUID("54b297e7-d68f-315b-e325-9daa275b81f7"), // Owner: Tatsuta Resident | Name: Forest screen 56x32x0.1-unvisible outside-phantom
			new UUID("a268426d-7293-acdf-ae01-6e3ca5b58a3e"), // Owner: Eo Fenstalker | Name: Turnip's Skydome 2.0 (90x90)
			new UUID("7162def2-894e-5a78-8fb7-f6e7c91f3acc"), // Owner: Fury Rosewood | Name: MindScapes - Cosmic Dream 64x64
			new UUID("ffaec309-bb7e-9e66-fad8-86d7b0ab22cb"), // Owner: Fury Rosewood | Name: TIS T1000
			new UUID("7a11ff31-128c-2e84-9c09-7345c6b721d8"), // Owner: Fury Rosewood | Name: 64x64x64
			new UUID("ba4760ca-52e5-5081-206c-934b19e5eb8d"), // Owner: Haley Maruti | Name: TIS T1000
			new UUID("1e1240d9-d036-d8f2-9fe1-97c790ed176b"), // Owner: Fury Rosewood | Name: 64x64x64
			new UUID("dcfaa61d-a5f9-7e8f-cda8-de20859382b1"), // Owner: Fury Rosewood | Name: 64x64x64
			new UUID("010f7445-a619-217f-535e-960d661cd840"), // Owner: Tatsuta Resident | Name: :Fanatik Architecture: NAKAMA 3

			// TROTSDALE:
			new UUID("d013b0c2-7646-35a3-9941-ff952c87f19a"), // Owner: Shurikenangel Resident | Name: Object
			new UUID("fb4bcbac-dfcb-ddb6-2b20-6880105aa257"), // Owner: Shurikenangel Resident | Name: Object
			new UUID("d0fb904a-7dcc-38ef-bb42-f7557380a277"), // Owner: Shurikenangel Resident | Name: PrimPossible Outdoor - 2 Prim Unlimited Palm Trees COPYABLE
			new UUID("f8f54dab-bc07-7945-ac32-0af66b1c1cf6"), // Owner: Shurikenangel Resident | Name: Waves beach
			new UUID("2b8bea2e-98af-84a3-cec1-05b81e9e5b7b"), // Owner: OldVamp Resident | Name: dirt
			new UUID("7b0d7be6-5d54-bf65-aa98-4e1769c85237"), // Owner: Nicelus Borrelly | Name: Object
			new UUID("d2860a7c-d058-7659-4fea-fe6f1fba8904"), // Owner: Nicelus Borrelly | Name: Object
			new UUID("275304a6-e835-b056-44fa-47e619896d0d"), // Owner: Nicelus Borrelly | Name: Object
			new UUID("a426e7bd-6e0c-4520-e1dd-c315970c2eae"), // Owner: Nicelus Borrelly | Name: Object
			new UUID("a361ecb3-2fdb-e256-7dd4-baa4e0318659"), // Owner: Shurikenangel Resident | Name: Promo  ....8 Palms 2Prim
			new UUID("db06e6f4-f4f0-f338-bc03-7c6402f94edd"), // Owner: Shurikenangel Resident | Name: PrimPossible Outdoor - 2 Prim Unlimited Palm Trees COPYABLE
			new UUID("836aed9e-3d2e-840e-7210-be3d3fbc2d7d"), // Owner: ScentualLust Resident | Name: palms_cutoff_by_Yorik_van_Havre
			new UUID("72a3a6e3-f7de-1441-de39-37469ae5a499"), // Owner: ScentualLust Resident | Name: Derpyland Walkway 2/4
			new UUID("c93a6ab0-1d92-0d66-e924-64a141e78d98"), // Owner: ScentualLust Resident | Name: Derpyland Bridge 3/3
			new UUID("00d8b130-70fb-f9a8-c009-eb7b05a5ab1b"), // Owner: Shurikenangel Resident | Name: PrimPossible Outdoor - 2 Prim Unlimited Palm Trees COPYABLE
			new UUID("6c5df93d-23e2-d093-7123-ec71665382e4"), // Owner: Shurikenangel Resident | Name: PrimPossible Outdoor - 2 Prim Unlimited Palm Trees COPY Dec2013
			new UUID("ebcd2b95-4d7b-78e3-669a-52a52420886a"), // Owner: Shurikenangel Resident | Name: PrimPossible Outdoor - 2 Prim Unlimited Palm Trees COPYABLE
			new UUID("0562256a-9640-87ec-f6cb-eb40821b7d1e"), // Owner: OldVamp Resident | Name: ramp
			new UUID("b7eeb393-dd4f-0fe1-2732-d1b616b32568"), // Owner: Nicelus Borrelly | Name: Object
			new UUID("56675d24-d091-6a89-819e-dea6f8557424"), // Owner: OldVamp Resident | Name: market lot
			new UUID("48352524-38a7-3ae4-5ffb-e62b57b72ba0"), // Owner: ScentualLust Resident | Name: Derpyland Walkway 4/4
			new UUID("07af591e-9950-5632-133a-982f7b10a995"), // Owner: OldVamp Resident | Name: roof
			new UUID("33eab96f-a606-739a-9c14-f62170a9e582"), // Owner: OldVamp Resident | Name: trim
			new UUID("fc26210e-73ee-e5eb-4d52-f4bdbba4d4d2"), // Owner: OldVamp Resident | Name: offlinkd road
			new UUID("50c00b7b-f1cd-b668-6386-9061229ec85c"), // Owner: OldVamp Resident | Name: ramp
			new UUID("79c8db0c-6417-0016-f9cd-bab238ab3944"), // Owner: OldVamp Resident | Name: wall
			new UUID("2a3cb6ed-10d1-65df-b18d-b5db9c0c1726"), // Owner: OldVamp Resident | Name: trim
			new UUID("97bc8b8f-5f41-de9a-cfbf-c33fd6d08bfa"), // Owner: OldVamp Resident | Name: wall
			new UUID("702b6e34-0b87-30f5-330b-3658c4544d91"), // Owner: OldVamp Resident | Name: floor
			new UUID("406208c5-ce24-f16c-a89e-9d4c616e1ec7"), // Owner: OldVamp Resident | Name: floor
			new UUID("8f7b5f5b-4006-a06a-2233-4c6f0a6f126c"), // Owner: OldVamp Resident | Name: floor
			new UUID("9d109a40-3564-7fd7-0c00-816ed6e442a9"), // Owner: ScentualLust Resident | Name:  NOM SQUARE Derpyland Square
			new UUID("8e33c778-2cd8-edab-89bc-3eace149f0fc"), // Owner: Shurikenangel Resident | Name: Object
			new UUID("3b21124b-7ecd-dfae-c986-a475aaac9885"), // Owner: Nicelus Borrelly | Name: Object
			new UUID("5ee0dd25-085f-3488-3e08-9e4ccf40928c"), // Owner: Shurikenangel Resident | Name: Tropical privacy screen (32x10)
			new UUID("4db2a1e2-b54b-217e-bcac-edb28b5c1a74"), // Owner: Shurikenangel Resident | Name: Tropical privacy screen (32x10)
			new UUID("63bc5332-8c98-9175-6235-0c5e3837f287"), // Owner: Shurikenangel Resident | Name: [Fox Labs] 16 Palm Freebie
			new UUID("6c0178e3-20ad-d23a-0771-53b686bbc23a"), // Owner: Kerry Giha | Name: Object
			new UUID("b16b13ad-ddb9-b250-8843-2eff2d6b0555"), // Owner: Xanaeth Epin | Name: Object
			new UUID("95fd9282-9999-6c64-1a8e-1edc4b5f3b10"), // Owner: Xanaeth Epin | Name: Object
			new UUID("4f133a1a-a4d6-b681-cb10-f8f5009a7a35"), // Owner: Valinye Resident | Name: Path
			new UUID("5b42f181-b192-03b0-541d-e9b78e39762d"), // Owner: Xanaeth Epin | Name: Object
			new UUID("d3960634-d649-e294-1935-38e83b7b77a9"), // Owner: Valinye Resident | Name: Path
			new UUID("411f1c68-c644-e38a-0438-d69e9d4c0181"), // Owner: Kerry Giha | Name: Object
			new UUID("a8b39698-0b04-c9f3-ec37-5201838b0107"), // Owner: Xanaeth Epin | Name: Object
			new UUID("62d6bbc8-ec14-155b-6625-23ae4341218e"), // Owner: Valinye Resident | Name: Path
			new UUID("cfc31c93-d2b4-4caa-73fe-54a5617175fa"), // Owner: Xanaeth Epin | Name: Object
			new UUID("679b1193-b67f-9409-3b07-e3058560e2aa"), // Owner: Valinye Resident | Name: Path
			new UUID("34de3e1e-81f8-c15c-a240-92f622b3ce8b"), // Owner: Crim Mip | Name: Magic Sparkle Rainbow 30x60
			new UUID("5cf31806-e76d-7291-4092-6f0c6b0f2f1f"), // Owner: Death Berger | Name: .:buddhabeats:. Aurora Borealis 2.0 intense
			new UUID("176dc762-40b8-14ce-1527-8d1c6f0ce695"), // Owner: NuzzyLus Resident | Name: Object
			new UUID("2c6fac6d-358b-3305-8ddb-7460d2852966"), // Owner: Falcon Rayna | Name: Object
			new UUID("52c44c82-3405-4eac-5c00-37dab8367de8"), // Owner: Xanaeth Epin | Name: Object
			new UUID("046ac771-1ce4-29a4-ccc0-c84cee00da03"), // Owner: Lacy Musketeer | Name: Oak 3
			new UUID("b3a199e9-70dd-c098-aee1-6b9ca2f7ad7d"), // Owner: Valinye Resident | Name: Path
			new UUID("82ede507-d8ff-950c-4f1a-9ee8c62d8d6f"), // Owner: Valinye Resident | Name: Path
			new UUID("30121102-9c49-b492-8f21-4febad17100a"), // Owner: Falcon Rayna | Name: Object
			new UUID("1d504c93-2d82-4118-d53f-cd8523de3047"), // Owner: Xanaeth Epin | Name: 256x256x0.5
			new UUID("3fb81770-18bf-ca07-977c-ebd4d39d0810"), // Owner: Cera Cyannis | Name: Awesome Rainbow
			new UUID("a8836a8a-628a-8ff8-8afd-201bb52e3c1f"), // Owner: Lacy Musketeer | Name: Oak 3
			new UUID("262c568b-4edc-a737-6b1a-6def85432ace"), // Owner: Cera Cyannis | Name: Awesome Rainbow
			new UUID("681f5621-aee1-232d-c2ba-2e96db6324e8"), // Owner: shippo849 Resident | Name: Canterlot Cliff everfree CM
			new UUID("d43e7dba-95f9-5b71-a40c-aa7d950dd7a5"), // Owner: Lacy Musketeer | Name: Oak 3
			new UUID("35172c6e-ae1d-188e-7d05-6a2b0a13a85f"), // Owner: Valinye Resident | Name: Path
			new UUID("01ab289a-2371-b35a-9415-6c09516b9e15"), // Owner: Valinye Resident | Name: Path
			new UUID("d1b9d09b-a1b2-c5ff-d6f7-e6a788ba9685"), // Owner: Valinye Resident | Name: Path
			new UUID("613ac5a0-fa4d-1b7e-08dc-3a27f537c68b"), // Owner: Valinye Resident | Name: Path
			new UUID("c2d23f89-433b-0f0b-447d-6eacc3c175cd"), // Owner: Valinye Resident | Name: Path
			new UUID("b9bdf9ff-1e6a-782f-23ad-574d4167b706"), // Owner: Valinye Resident | Name: Path
			new UUID("b5405f0a-4689-3d99-5ce6-8ce6a44b1bb2"), // Owner: Ryverwind Eourres | Name: Skyroom - Atlantea
			new UUID("b13ea953-2851-c478-c2e4-17e43485b408"), // Owner: Xanaeth Epin | Name: Object
			new UUID("6f5d8ade-6b65-3dac-6839-980b3659a355"), // Owner: Xanaeth Epin | Name: Object
			new UUID("eba9d169-acac-0f56-364e-6918039e8f3f"), // Owner: Oscelot Haalan | Name: Palace - floor collision
			new UUID("3ee25675-fa09-417d-26ae-8ce93ba7ec24"), // Owner: Crim Mip | Name: [FYI] Cave Entrance 2
			new UUID("7a665455-7b61-d86c-3942-b8bae268e457"), // Owner: Amehana Ishtari | Name: stone wall
			new UUID("f9c899cb-32d2-8a40-e8b2-8f0199a9a2ab"), // Owner: Crim Mip | Name: The Real Aurora (huge + tall version)
			new UUID("06ba965f-fa29-ac95-2006-4e5f09336f1a"), // Owner: Oscelot Haalan | Name: Object
			new UUID("03533624-f644-0bcd-277f-12ca5ae36fe0"), // Owner: Lacy Musketeer | Name: Mount Morgan
			new UUID("b6244d2c-29ff-a236-6aa7-34c702b8867a"), // Owner: Amehana Ishtari | Name: Created Using JVTEK LandMap
			new UUID("71e08b18-bb9c-c077-b6c7-08b906f5d398"), // Owner: Xanaeth Epin | Name: Object
			new UUID("9c620187-a7b8-4e4a-8498-2f106349e592"), // Owner: Oscelot Haalan | Name: Object
			new UUID("d167a914-725b-bc6b-3bdd-46859dcc2ca7"), // Owner: Crim Mip | Name: [FYI] Pro Cave Tunnel Kit Flat Straight
			new UUID("5e560d03-2e64-366d-a309-5edc3ad4b2f5"), // Owner: Lacy Musketeer | Name: [FYI] Basin 03
			new UUID("b1387c7e-84a8-be4a-eb44-deeea5c77e88"), // Owner: Crim Mip | Name: [FYI] Basin 01
			new UUID("98b47c67-ab8a-45e3-05a8-00e6225f7a36"), // Owner: Amehana Ishtari | Name: Created Using JVTEK LandMap
			new UUID("1b2c3134-3c1b-03f7-9777-fc9c75c28863"), // Owner: Amehana Ishtari | Name: Created Using JVTEK LandMap
			new UUID("69125f6b-6dcf-ef15-6f70-fe396c7bcd7f"), // Owner: Crim Mip | Name: [FYI] Hill Base 03
			new UUID("4e8dbaec-389f-3671-f727-e333aaedc63e"), // Owner: Crim Mip | Name: [FYI] Pro Cave Tunnel Kit Flat Straight
			new UUID("d2a974cb-238c-091a-8901-ab9048e52d4c"), // Owner: Crim Mip | Name: [FYI] Cave Entrance 2
			new UUID("74773c15-dbf9-99b7-73bb-2ee77ba69dd1"), // Owner: Amehana Ishtari | Name: Created Using JVTEK LandMap
			new UUID("be298912-52f7-2fc6-9233-63aef7041e2a"), // Owner: Lacy Musketeer | Name: Terrain_03_COOL
			new UUID("7c02b631-3da3-11bc-04ec-f279f4ea1106"), // Owner: Lacy Musketeer | Name: Object
			new UUID("22d7c90c-e6bd-a6e5-a7e1-5f63611e3fc6"), // Owner: Lacy Musketeer | Name: Terrain_03 ( Sculpt ) 64x64
			new UUID("7df6b514-efa0-61de-b2a2-bff6f42741c2"), // Owner: Lacy Musketeer | Name: [FYI] Flat Hills 02
			new UUID("d90523cd-6ce4-3479-af42-a5815c687da2"), // Owner: Crim Mip | Name: !Pandemonium Mist Ground Scripted ModV
			new UUID("b989428b-c9c4-82b8-0e56-a13b7bf48892"), // Owner: Lacy Musketeer | Name: Object
			new UUID("b8a1c60a-10f4-8d64-abc2-c40e834c0d59"), // Owner: Lacy Musketeer | Name: banyan tree 18 prims M/T
			new UUID("8afd83cb-16b0-15ff-daec-91df2ef49712"), // Owner: Crim Mip | Name: !Pandemonium Mist Ground Scripted ModV
			new UUID("94fd31e4-6a2c-7dfd-9b9c-d25bc2b5625b"), // Owner: Crim Mip | Name: !Pandemonium Mist Ground Scripted ModV
			new UUID("a075a161-015a-8d84-35fe-929904f4b69f"), // Owner: Lacy Musketeer | Name: Object
			new UUID("97036c9d-713e-33b9-6da6-d169c036866b"), // Owner: Crim Mip | Name: !Pandemonium Mist Ground Scripted ModV
			new UUID("ca45e575-a7f9-61f1-b457-3b59c7d45f24"), // Owner: Crim Mip | Name: !Pandemonium Mist Ground Scripted ModV
			new UUID("7fbf4cf5-0ed7-9c01-721e-05259053d881"), // Owner: Lacy Musketeer | Name: Object
			new UUID("2f3626b8-9fe4-ad63-2cc5-dabf50c54973"), // Owner: Lacy Musketeer | Name: Terrain_03 ( Sculpt ) 64x64
			new UUID("e0fe649e-189d-3107-dc2b-24764691b90d"), // Owner: Lacy Musketeer | Name: QwhillDrassil Basin Terrain
			new UUID("51ed9408-4572-3b67-fbee-baeeb511b29b"), // Owner: Lacy Musketeer | Name: Object
			new UUID("74d72be7-7d6a-4f1f-51ce-2a7fc4398d9b"), // Owner: Lacy Musketeer | Name: Terrain_03_COOL
			new UUID("d6d491ff-1cf7-d05b-08a9-57a9a3feda5a"), // Owner: Lacy Musketeer | Name: QwhillDrassil Basin 
			new UUID("18e056a8-913a-9c12-32c6-b8fabda39f4a"), // Owner: Lacy Musketeer | Name: Object
			new UUID("677c3258-5ca6-bc34-08b2-8414b9fbbafb"), // Owner: Lacy Musketeer | Name: Oak 2
			new UUID("fbc60025-bb16-8ec9-21ed-7fe67457ba57"), // Owner: Shurikenangel Resident | Name: Real Waves model Ocean Wave - Version 2012 T39-A
			new UUID("69ae5e65-b27b-a500-8ee9-416e5c548bb2"), // Owner: Shurikenangel Resident | Name: @deeps@ seaweed, wide area 10
			new UUID("ed8d1534-1042-74e7-5f76-abdb03b316e0"), // Owner: Shurikenangel Resident | Name: PrimPossible Outdoor - 2 Prim Unlimited Palm Trees COPYABLE
			new UUID("216d4589-f2e9-045d-b3b6-917071658531"), // Owner: Shurikenangel Resident | Name: PrimPossible Outdoor - 2 Prim Unlimited Palm Trees COPY Dec2013
			new UUID("9b6db134-8cb5-33e9-7f6e-3641d947e003"), // Owner: Shurikenangel Resident | Name: Real Waves model Ocean Wave - Version 2012 T39-A
			new UUID("7ed64e1a-b8b6-7b55-1b54-ffe15d5553b0"), // Owner: Shurikenangel Resident | Name: PrimPossible Outdoor - 2 Prim Unlimited Palm Trees COPYABLE
			new UUID("4b1d3cf3-f483-f24f-2300-21d6d849f9d3"), // Owner: ScentualLust Resident | Name: Derpyland Walkway 3/4
			new UUID("4e60aa93-becb-1d3e-5ce8-9589d851af6c"), // Owner: ScentualLust Resident | Name: palms_cutoff_by_Yorik_van_Havre
			new UUID("6a30cae1-903b-e76f-1941-a9bcf9bd7763"), // Owner: Shurikenangel Resident | Name: Waves Island
			new UUID("83dfbdb7-7f8e-ff8c-76ab-2b03d7ccb755"), // Owner: Shurikenangel Resident | Name: Q Creations - Distant Island 1
			new UUID("12401882-7434-2dbe-4bf6-2b52184210e8"), // Owner: Shurikenangel Resident | Name: @deeps@ seaweed, wide area 10
			new UUID("5e97041e-2567-2a22-41c0-5470b1ecb2a4"), // Owner: ScentualLust Resident | Name: palms_cutoff_by_Yorik_van_Havre
			new UUID("a81bcff6-af69-e4b8-cb20-2efaff818d57"), // Owner: Shurikenangel Resident | Name: PrimPossible Outdoor - 2 Prim Unlimited Palm Trees COPY Dec2013
			new UUID("e4aa9979-ec22-b769-b8a2-c2a90daef8ae"), // Owner: shippo849 Resident | Name: Moon Skybox w/Earth 64x64x64 (5p)
			new UUID("4159da6f-3784-5bf5-aa09-d1a4dbd44a7c"), // Owner: Shurikenangel Resident | Name: PrimPossible Outdoor - 2 Prim Unlimited Palm Trees COPYABLE
			new UUID("503952f6-4d33-6473-7da8-b092eb968625"), // Owner: Shurikenangel Resident | Name: PrimPossible Outdoor - 2 Prim Unlimited Palm Trees COPYABLE
			new UUID("7bcf68f6-9025-9d86-eaa6-83356fe0d184"), // Owner: Shurikenangel Resident | Name: Object
			new UUID("d034e226-d823-d197-a37c-763f536a77d6"), // Owner: Shurikenangel Resident | Name: PrimPossible Outdoor - 2 Prim Unlimited Palm Trees COPYABLE
			new UUID("bdb1a16f-4d25-dc7c-06b8-a5ee214dbda2"), // Owner: Coolman187 Resident | Name: Object
			new UUID("d322ee87-17bc-1e77-5838-d022d1dc4217"), // Owner: Xanaeth Epin | Name: Object
			new UUID("6db64005-8eb1-e9c1-a92c-e14fe391a034"), // Owner: Xanaeth Epin | Name: Object
			new UUID("17b81200-9ffc-62d6-91d5-a6795c80ec01"), // Owner: Xanaeth Epin | Name: Object
			new UUID("618b4514-8369-51aa-bc77-2fc98a882ed0"), // Owner: Xanaeth Epin | Name: Object
			new UUID("0e7ed089-f855-7c92-aa00-0c9965c4b331"), // Owner: Xanaeth Epin | Name: Object
			new UUID("ff6dc23c-a490-8e4a-ab06-39d11d854e81"), // Owner: Xanaeth Epin | Name: Object
			new UUID("3b61b183-165a-7aff-408e-6ba42f451197"), // Owner: Xanaeth Epin | Name: Object
			new UUID("ccf27b0c-7989-cab9-b3b2-245e7f28b198"), // Owner: Xanaeth Epin | Name: Object
			new UUID("d6792870-77a7-0fe8-18ab-6f354a1e5f1e"), // Owner: Xanaeth Epin | Name: Object
			new UUID("2d511683-2c9e-391d-31ad-11e9747e5900"), // Owner: Xanaeth Epin | Name: Object
			new UUID("c97ff471-a107-d2a4-8030-129078116085"), // Owner: Shurikenangel Resident | Name: PrimPossible Outdoor - 2 Prim Unlimited Palm Trees COPYABLE
			new UUID("eec57cf3-53e8-9dc2-f852-b28568f6aa0b"), // Owner: Crim Mip | Name: [FYI] Flat Hills 03
			new UUID("4132f230-730a-9287-dca2-7ed266321ac6"), // Owner: Amehana Ishtari | Name: ceiling
			new UUID("9e9fd32a-90ba-040e-77d0-51606568cbdb"), // Owner: Amehana Ishtari | Name: fog mesh v1
			new UUID("6bd911b6-2841-8ccb-f0c1-8d396fc53932"), // Owner: Lacy Musketeer | Name: Object
			new UUID("e5c81594-00c8-d8af-1784-d287c9826bda"), // Owner: Amehana Ishtari | Name: 2 prim mesh Tunnel.straight by felix mody/copy
			new UUID("81a89715-0b3a-2bb1-4033-098f46bfb339"), // Owner: Lacy Musketeer | Name: banyan tree 18 prims M/T
			new UUID("6d754509-44ef-ae0d-9052-635082935f76"), // Owner: Lacy Musketeer | Name: Oak 5
			new UUID("9188e5c8-bb96-4cf7-ed84-f474d086c172"), // Owner: Lacy Musketeer | Name: Oak 5
			new UUID("2337dcdd-fc71-03e9-6160-f942f4e70c8e"), // Owner: Lacy Musketeer | Name: Oak 3
			new UUID("446ec779-a9f7-a7a4-0e1c-3d0093ea73ee"), // Owner: Lacy Musketeer | Name: Oak 1
			new UUID("fa0e5d72-6228-c88e-0412-8ea422a0b23a"), // Owner: Lacy Musketeer | Name: Object
			new UUID("185aeb88-2726-b145-1f60-86434cf0bc41"), // Owner: Lacy Musketeer | Name: Oak 3
			new UUID("a492b68d-c0d5-a1ba-523f-cfbeffcd23c6"), // Owner: Lacy Musketeer | Name: Oak 3
			new UUID("d0b874b1-4547-303d-e080-ce3f8cf531e9"), // Owner: Lacy Musketeer | Name: Oak
			new UUID("a42cee77-efaa-1cf8-2b5a-fafc70db0715"), // Owner: Lacy Musketeer | Name: Oak 5
			new UUID("9ebad47d-5a6b-c1fb-6402-675cf463843d"), // Owner: Lacy Musketeer | Name: 85x85x60
			new UUID("58f7c873-d494-ec03-583f-5fb2f4129362"), // Owner: Lacy Musketeer | Name: Oak 2
			new UUID("b6dd6e15-4c2d-6b8a-751c-cfbf06f2496a"), // Owner: Lacy Musketeer | Name: Oak 3
			new UUID("9ce601cc-c6e4-b9cb-f425-e4b8003f44bb"), // Owner: Lacy Musketeer | Name: Oak 3
			new UUID("fef8e2fe-2b14-ec39-c4aa-9acab74d2146"), // Owner: Lacy Musketeer | Name: Oak 3
			new UUID("4459fefd-db71-6aba-c062-c0bd09d884a3"), // Owner: Lacy Musketeer | Name: Object
			new UUID("27c8b344-8eac-0b49-2a43-8476e8017693"), // Owner: Lacy Musketeer | Name: Oak 2
			new UUID("84a36ced-38ab-f4b7-74b0-fb5b24b2cd99"), // Owner: Lacy Musketeer | Name: Object
			new UUID("ca4ee87c-49dc-9906-569b-0187482d1c99"), // Owner: Lacy Musketeer | Name: Sunbeam
			new UUID("5f7e9573-e979-51b5-176c-3195d9e7a407"), // Owner: Lacy Musketeer | Name: Oak
			new UUID("b28c371c-f521-15c8-e996-d7ac8218a5f1"), // Owner: Lacy Musketeer | Name: [FYI] Basin 03
			new UUID("f333c5d2-d2e6-57ba-c12e-d9b9f83122a6"), // Owner: Crim Mip | Name: Object
			new UUID("1dd44815-de64-ecb9-ac60-8ab3cdca1c4c"), // Owner: Kerry Giha | Name: Event Area Floor
			new UUID("b196f9d6-539a-c622-77a5-7602459580e5"), // Owner: Crim Mip | Name: Event Area Guard Rail
			new UUID("c5242959-f639-4898-5f0e-5b097a4b63b2"), // Owner: Lacy Musketeer | Name: Oak
			new UUID("0c1ce5e9-d832-0127-bc59-c594eb653289"), // Owner: Lacy Musketeer | Name: [FYI] Basin 03
			new UUID("dcedba03-77fa-5d19-e072-18acbc893da5"), // Owner: Oscelot Haalan | Name: Botanical - Aurora Borealis 1.1 (copy)
			new UUID("1b102748-9d23-5085-992c-a6a98a64adab"), // Owner: Crim Mip | Name: Event Area Guard Rail
			new UUID("627dcbea-b698-d60c-b90c-efb3b20d770f"), // Owner: Lacy Musketeer | Name: Oak
			new UUID("d55b2c70-6c81-7b20-8829-2d049f4961df"), // Owner: Lacy Musketeer | Name: Oak
			new UUID("9acc3a9f-2587-49a1-57e7-8e655ac8c45f"), // Owner: Lacy Musketeer | Name: Object
			new UUID("c338d5fb-b7c1-28f6-17e8-5e2ad320f0a4"), // Owner: Amehana Ishtari | Name: Object
			new UUID("58ed7f4b-73d5-0fb1-4408-a58c43a5ff63"), // Owner: Crim Mip | Name: Object
			new UUID("0b43686b-5c5f-dabd-f0ef-8bc0ac1d7731"), // Owner: Kerry Giha | Name: [FYI] Cave Builder Kit Floor/Ceiling 4
			new UUID("ca3d9e4d-5fa5-5bc7-cb4a-94f9b86b116f"), // Owner: Amehana Ishtari | Name: Created Using JVTEK LandMap
			new UUID("756110f9-5232-e90d-dadd-c1c463d10eeb"), // Owner: Lacy Musketeer | Name: [FYI] Dip Three Corners
			new UUID("e6f77920-ee0f-0980-bd48-227b8905c251"), // Owner: Kerry Giha | Name: [FYI] Flat Hills 04
			new UUID("9ebf920f-39af-6eea-40c7-ff8c0e8eddfc"), // Owner: Kerry Giha | Name: [FYI] Cave Builder Kit Floor/Ceiling 4
			new UUID("72885cab-4ead-2b3c-c984-78b577d05a4e"), // Owner: Lacy Musketeer | Name: willow curved 4prims M/T
			new UUID("5a4eb777-b682-7f3b-d9b8-7705cccd2222"), // Owner: Kerry Giha | Name: Object
			new UUID("2d60e4dd-1a62-bf16-b054-e48107f233d7"), // Owner: Kerry Giha | Name: Oak
			new UUID("a0545f8f-ab5b-6898-3351-5f7112c87e13"), // Owner: FluffyQuokka Resident | Name: Butterfly Spring Cave 60 Prim 30x30 m Footprim 1
			new UUID("218b56cd-196a-a1b3-4e34-32fbdca79d5a"), // Owner: Kerry Giha | Name: [FYI] Pro Cave Tunnel Kit Flat Straight
			new UUID("a44d5fd4-d6bc-0205-3b62-37bf0cb9a5ee"), // Owner: Lacy Musketeer | Name: Hayabusa Design Arch 3 Trees F3aWT t263v1
			new UUID("a054f8c3-98d5-34c7-f88c-cb646c7a092c"), // Owner: Kerry Giha | Name: d
			new UUID("dd4132ab-407e-aa5f-fd96-1636ab1f500b"), // Owner: Kerry Giha | Name: [FYI] Mesh Rimerock Cave 1.0.2
			new UUID("d3a7e579-8c04-8fdc-f547-58dcab180c64"), // Owner: Crim Mip | Name: [FYI] Pro Cave Tunnel Kit Entrance 4
			new UUID("97dac59c-c925-8bb4-6a63-f157e27a0982"), // Owner: Kerry Giha | Name: Object
			new UUID("87c9564d-312f-e92b-2b19-edff1840e38b"), // Owner: Kerry Giha | Name: [FYI] Flat Hills 04
			new UUID("7715c02b-ed11-77b9-0386-01e0e002808a"), // Owner: Kerry Giha | Name: [FYI] Flat Hills 04
			new UUID("68c93133-859e-549c-1090-8bbe43ccc736"), // Owner: Celano Obscure | Name: Butterfly Spring Cave 60 Prim 30x30 m Footprim 1
			new UUID("b783dab9-da4a-33dc-8112-939c962f0208"), // Owner: Kerry Giha | Name: [FYI] Pro Cave Tunnel Kit Entrance 4
			new UUID("f4152c39-63a6-5cf3-cad8-3d965fe2df7d"), // Owner: Lacy Musketeer | Name: 150x150x85
			new UUID("9e9a00e4-a06e-1984-e47d-def51c092f28"), // Owner: Kerry Giha | Name: [FYI] Pro Cave Tunnel Kit Flat Straight
			new UUID("17a7fe36-7dca-17d0-ee80-122c735a1c09"), // Owner: Lacy Musketeer | Name: Object
			new UUID("39bebc75-37c3-f3dd-bf5f-648144bc043c"), // Owner: Kerry Giha | Name: [FYI] Hill Top 02
			new UUID("a2c72a4c-f750-9f90-b758-e2d39831cd8d"), // Owner: Lacy Musketeer | Name: 95x95x125
			new UUID("956be2dc-b87a-dc3a-4864-271ab4d3fa3b"), // Owner: Kerry Giha | Name: [FYI] Hill Top 02
			new UUID("ed8ff1d9-253b-537f-d4e8-cf7d67e3866d"), // Owner: Kerry Giha | Name: [FYI] Pro Cave Tunnel Kit Flat Straight
			new UUID("7d1dd964-1b9e-0691-2a7b-572b611e4d1b"), // Owner: Kerry Giha | Name: [FYI] Hill Top 02
			new UUID("4cbdb206-3d88-5521-627c-b7fe00bce4d9"), // Owner: Lacy Musketeer | Name: 150x150x85
			new UUID("3e5ec0c6-6a64-cea9-06ff-3eb3ce046a5c"), // Owner: Kerry Giha | Name: [FYI] Flat Hills 04
			new UUID("088f767a-b387-9a9a-11a9-415a66e71535"), // Owner: Kerry Giha | Name: [FYI] Hill Top 02
			new UUID("55cc9f9e-5626-27c4-4f3e-db9c78253952"), // Owner: Kerry Giha | Name: [FYI] Hill Top 02
			new UUID("c512e5c9-8de7-33c1-88d6-1ee1943a1187"), // Owner: Lacy Musketeer | Name: 150x150x85
			new UUID("633e9dd3-9768-f010-dd4a-e1c96c6cac33"), // Owner: Lacy Musketeer | Name: 100x100x70
			new UUID("44825aad-e477-8693-ab55-d4f6a431aece"), // Owner: Lacy Musketeer | Name: 80x80x125
			new UUID("b931d694-6ad0-e53d-0dc2-f0dc0c9f8baf"), // Owner: Kerry Giha | Name: [FYI] Hill Top 02
			new UUID("042ff2d8-dcad-316b-e713-56ef50e372cd"), // Owner: Kerry Giha | Name: Object
			new UUID("3cbda7fe-a349-2ef3-d5ba-fd65b82310f4"), // Owner: Kerry Giha | Name: Object
			new UUID("2572da7d-7fcd-de08-36f2-69483b975ca2"), // Owner: Lacy Musketeer | Name: 100x100x70
			new UUID("75f8ceb1-c263-a7ae-1ab1-e6cc58ab2049"), // Owner: Kerry Giha | Name: Object
			new UUID("d19c878a-dfce-6551-df18-125dc198591d"), // Owner: Lacy Musketeer | Name: Sunbeam
			new UUID("adf70c83-9a15-2b15-c24a-812abeafb070"), // Owner: Lacy Musketeer | Name: 150x150x85
			new UUID("a6bb7dfc-ac8b-1fa0-f3eb-bcd103e28a2f"), // Owner: Lacy Musketeer | Name: 95x95x125
			new UUID("68d6d74a-9f37-9428-9f78-f0039b70d200"), // Owner: Kerry Giha | Name: Object
			new UUID("4d27a7c2-9b99-c9b4-9a99-df9c3bc0c00e"), // Owner: Lacy Musketeer | Name: Sunbeam
			new UUID("1b127e22-b5bb-80bc-9921-0073b0823e79"), // Owner: Lacy Musketeer | Name: 100x100x70
			new UUID("f010a2d1-f84d-bb9f-a754-eec040fec8dd"), // Owner: Kerry Giha | Name: [FYI] Mesh Waterfall S1
			new UUID("9c50a87f-7200-7aa1-aaba-c33e93c8b41a"), // Owner: Lacy Musketeer | Name: 150x150x85
			new UUID("62c2da83-c2b1-67b1-145c-47ab4f8a4ed4"), // Owner: Lacy Musketeer | Name: Sunbeam
			new UUID("8a8da40a-9d2e-e73a-2405-58210d90ee97"), // Owner: Lacy Musketeer | Name: 95x95x125
			new UUID("9d34a900-b772-000e-3bf0-b4fd8f329738"), // Owner: Kerry Giha | Name: Object
			new UUID("e5708689-c4b6-2897-e7d6-8c879217db8e"), // Owner: Lacy Musketeer | Name: 100x100x70
			new UUID("b2461574-1b3e-0b06-0028-ec6f59ebdd71"), // Owner: Lacy Musketeer | Name: 100x100x70
			new UUID("8bc25f76-8852-c412-f9ab-6df251077624"), // Owner: Lacy Musketeer | Name: 150x150x85
			new UUID("b1af79ce-15aa-1295-8b98-ac9da22f4d82"), // Owner: Lacy Musketeer | Name: Object
			new UUID("99782cd8-49a1-eaba-e9b6-313281802c58"), // Owner: Lacy Musketeer | Name: 150x150x85
			new UUID("5cd50bd1-b3a2-c158-8d53-df59c3c8a83d"), // Owner: Lacy Musketeer | Name: 100x100x70
			new UUID("253b0d9e-c1a4-df2b-3356-4771093f9459"), // Owner: Lacy Musketeer | Name: Object
			new UUID("28d429b7-e095-e9b3-1fec-02345df81722"), // Owner: Lacy Musketeer | Name: Object
			new UUID("546665cf-eb6b-8c46-bde1-ce8769384cd8"), // Owner: Lacy Musketeer | Name: Object
			new UUID("76290602-2afa-498c-cca1-81df21c5998a"), // Owner: Lacy Musketeer | Name: 100x100x70
			new UUID("f0527df5-cffe-a21c-904c-9224352f3d43"), // Owner: Lacy Musketeer | Name: Object
			new UUID("f28a03d9-3f80-aecf-2506-7731cfad9d42"), // Owner: Lacy Musketeer | Name: Object
			new UUID("4144fecb-287d-c5ad-c837-3fd01752bd67"), // Owner: MeowGuy Resident | Name: Turnip's Skydome 2.0 (40x40)
			new UUID("1c26f4dd-6c52-0d57-c31c-d9d94f8e72e3"), // Owner: OldVamp Resident | Name: The Moon
			new UUID("1607c715-9340-4b15-fd77-104b6329175e"), // Owner: OldVamp Resident | Name: moonrail
			new UUID("a4cb8726-16e3-8d50-6582-0498ffa33c43"), // Owner: OldVamp Resident | Name: platform
			new UUID("a05f01fb-79d3-ed3c-e5a4-3d6d59f637b6"), // Owner: AnyaMaru Resident | Name: Object
			new UUID("84896a7a-053b-6fa6-d429-0ea420bc90b8"), // Owner: Crim Mip | Name: Big Bang Box 2.42
			new UUID("b56e7df5-f597-8043-aa16-823ef99935fd"), // Owner: Shurikenangel Resident | Name: Tropical privacy screen (32x10)
			new UUID("40958e0a-57d3-ad71-60b4-9c053f640ce6"), // Owner: Shurikenangel Resident | Name: Object
			new UUID("64b4beb0-1d5a-a6e3-d92d-1b7700e7ff79"), // Owner: Xanaeth Epin | Name: SAA-04
			new UUID("ec44cb7f-fe8d-6d1e-e6e7-749b3773b581"), // Owner: Shurikenangel Resident | Name: Tropical privacy screen (32x10)
			new UUID("5f8c3e2e-e414-7ca4-939f-da14130b48fd"), // Owner: Xanaeth Epin | Name: Object
			new UUID("90128c31-07e3-c545-5fd3-eea7aa9d435a"), // Owner: Xanaeth Epin | Name: Object
			new UUID("5accc7dc-646d-41ac-6735-2e307fdca16e"), // Owner: Lacy Musketeer | Name: Object
			new UUID("8f20cec8-c085-486b-64a9-c2b63c67d6e1"), // Owner: Xanaeth Epin | Name: Object
			new UUID("a672bb91-baa2-8bc9-898c-82a0df1dd571"), // Owner: Xanaeth Epin | Name: Object
			new UUID("33b5dd6d-6c90-fb98-00f1-c6884dd75f92"), // Owner: Crim Mip | Name: :: BBI :: Forseti Apartment Pod 1.0.1 Purple
			new UUID("47601a0e-33e3-56c9-2b2a-3d567d9e33f5"), // Owner: Xanaeth Epin | Name: shadermesh
			new UUID("dc5fb69a-931e-1efb-b814-73440fe6100b"), // Owner: Xanaeth Epin | Name: Object
			new UUID("dee62e84-0030-7aea-6d7f-8b823eed8ed5"), // Owner: Xanaeth Epin | Name: Plane.005
			new UUID("01e3eb58-22f4-a07b-0a58-5ae13deea357"), // Owner: Xanaeth Epin | Name: Object
			new UUID("9ea38874-b2ae-830e-4c30-beef140d662a"), // Owner: Xanaeth Epin | Name: Object
			new UUID("959ab509-09c4-a079-4eb1-ace23a9ec9ed"), // Owner: Xanaeth Epin | Name: Object
			new UUID("f88ab44f-831a-6636-a52b-248d1e41013f"), // Owner: Xanaeth Epin | Name: Object
			new UUID("f0534a11-2541-01f9-90bc-076f2c1daa6f"), // Owner: Xanaeth Epin | Name: Object
			new UUID("9b0576b3-7215-23d7-f09e-269ff4e89b9d"), // Owner: Xanaeth Epin | Name: Object
			new UUID("71c398a8-c0ae-ec02-17ea-5a6db7772890"), // Owner: Xanaeth Epin | Name: Object
			new UUID("be3d3796-1cf7-769c-32c9-b271a74bf7c0"), // Owner: Xanaeth Epin | Name: shadermesh
			new UUID("b5f2ebb2-4bf3-2609-2c34-9525793d9d53"), // Owner: Xanaeth Epin | Name: Object
			new UUID("ef739dbb-53ab-6bfe-ad1a-a79184692806"), // Owner: Xanaeth Epin | Name: shadermesh
			new UUID("fbda9f76-1d73-71a3-60f2-24e821df5c5c"), // Owner: Xanaeth Epin | Name: Object
			new UUID("d8f01aaf-61e5-5ca6-4665-e33402098926"), // Owner: Xanaeth Epin | Name: Object
			new UUID("d18f3206-3911-2150-a9c2-47a3064d77e6"), // Owner: Xanaeth Epin | Name: Object
			new UUID("4b1eb74e-d154-d503-f30e-b4233675c1bf"), // Owner: Xanaeth Epin | Name: Object
			new UUID("d1091b27-9353-61d7-c55a-d86674660d07"), // Owner: Xanaeth Epin | Name: Object
			new UUID("a15b7af8-8c3d-7081-c8f6-a6a246c5794d"), // Owner: Xanaeth Epin | Name: Object
			new UUID("fded331a-4404-0d50-3a81-cff3b52ab7b2"), // Owner: Xanaeth Epin | Name: Object
			new UUID("e24340c7-2cb1-7e9c-c6d2-ed639eb1dba4"), // Owner: Xanaeth Epin | Name: Object
			new UUID("f08b649d-9ef7-9ad6-5653-d15d04ca119f"), // Owner: Xanaeth Epin | Name: Object
			new UUID("f204221b-e939-d3de-fed3-5a04f0eb145e"), // Owner: Xanaeth Epin | Name: Object
			new UUID("3df41331-59b9-588d-aa79-161786124233"), // Owner: Xanaeth Epin | Name: Object
			new UUID("dcdb099d-45a8-9e82-5f1c-2a10f6682899"), // Owner: Xanaeth Epin | Name: Object
			new UUID("fd336b4b-f312-dd0d-4b4c-428dead50a90"), // Owner: Xanaeth Epin | Name: Object
			new UUID("1a569f84-2136-268d-ffe8-3b00c23d6381"), // Owner: Xanaeth Epin | Name: Object
			new UUID("716dc23f-b14c-56e2-0bd9-5fb9cdf7fc6d"), // Owner: Xanaeth Epin | Name: Object
			new UUID("954ba690-3846-4846-38a3-781f4b763b90"), // Owner: Xanaeth Epin | Name: Object
			new UUID("b97e2ebd-7898-0e36-6315-9a72e143d04b"), // Owner: Xanaeth Epin | Name: Object
			new UUID("cba7e1e9-7089-92cd-a857-06ece3fab657"), // Owner: Xanaeth Epin | Name: Object
			new UUID("87d4f73f-305a-a831-846c-6152851e2f58"), // Owner: Xanaeth Epin | Name: Object
			new UUID("38445c82-e4b8-bd8f-d4de-4628e6380a56"), // Owner: Xanaeth Epin | Name: Object
			new UUID("bc7564f4-dde9-c45b-4537-179b7b471747"), // Owner: Xanaeth Epin | Name: Object
			new UUID("ae34e959-4caf-91fe-b55d-402c778deebf"), // Owner: Xanaeth Epin | Name: EFF-mesh1
			new UUID("00ace2a0-288e-5593-724b-786d234e6f1c"), // Owner: Xanaeth Epin | Name: Object
			new UUID("3e802dcd-de16-7bf6-03f7-4b53aff2fa15"), // Owner: Xanaeth Epin | Name: Object
			new UUID("e86e7aa5-e9e4-dade-5401-82d7b1ee392d"), // Owner: Xanaeth Epin | Name: Plane.006
			new UUID("fded7419-3691-540f-19e8-f47dd0cb2157"), // Owner: Xanaeth Epin | Name: Object
			new UUID("f31ffc0e-a148-6963-0512-445d99c5fc81"), // Owner: Xanaeth Epin | Name: Object
			new UUID("770256b3-5580-dcb5-7037-f7968d6698ee"), // Owner: Xanaeth Epin | Name: Object
			new UUID("4a7bcf08-5913-9880-74ca-afcadd938b4c"), // Owner: Xanaeth Epin | Name: Object
			new UUID("f0b48d84-98a6-c05f-11d6-45b843b6e991"), // Owner: Xanaeth Epin | Name: EFF-mesh1
			new UUID("678552ae-df3f-7a56-de98-c8878d8b5f5f"), // Owner: Crim Mip | Name: rain
			new UUID("a8cfc802-3f5d-d9ae-4bed-f5d31597bd4a"), // Owner: Xanaeth Epin | Name: Object
			new UUID("ea4980dc-24c8-86ad-8948-57544c1fa070"), // Owner: Xanaeth Epin | Name: Object
			new UUID("f316ad3f-09f6-ec67-1a1e-bf54673e0f89"), // Owner: Xanaeth Epin | Name: Object
			new UUID("eb618886-866a-8a08-a1cc-14ad68dbfa0c"), // Owner: Xanaeth Epin | Name: trapdoorreplacer
			new UUID("a7a7a439-cff6-4cc3-6764-c9b85b9a176d"), // Owner: Xanaeth Epin | Name: Object
			new UUID("1caa5dbd-5c67-ce87-24d6-6bc78f5e8530"), // Owner: Crim Mip | Name: fog
			new UUID("b5d44803-8ec0-b735-bdca-1968e864dac4"), // Owner: Xanaeth Epin | Name: Object
			new UUID("1c379c07-0cc1-05ce-2644-1b30bc6b266a"), // Owner: Xanaeth Epin | Name: Object
			new UUID("9d6e1f98-fe7a-8210-4a22-b3e46e81686d"), // Owner: Crim Mip | Name: mist
		};

		private HashSet<string> regionsToCheckForObjects = new HashSet<string>()
		{
			// LUSKWOOD:
			"lusk",
			"perry",
			"tehama",
			"clara",

			// TROTSDALE:
			"trotsdale",
			"derpyland",
			"pony town",
			"exmoor",
			"nisa",
			"ibex empire",
			"qwhilldrasil",
			"thessalia",
		};

		private Dictionary<UUID, DateTime> previousRequests = new Dictionary<UUID, DateTime>();

		ManualResetEvent WaitForSessionStart = new ManualResetEvent(false);

		public void StartPlugin(RadegastInstance inst)
		{
			instance = inst;

			Output("PrimScanner loaded!");

			requestedUpdates.Clear();
			WaitForSessionStart.Reset();

			instance.Client.Objects.ObjectUpdate += Objects_ObjectUpdate;
			instance.Client.Objects.ObjectProperties += Objects_ObjectProperties;
			instance.Client.Self.GroupChatJoined += Self_GroupChatJoined;

			//clearPreviousRequestsTimer = new Timer(clearPreviousRequestsTimerCallback, null, TimeSpan.FromSeconds(20), TimeSpan.FromSeconds(20));
			ScanAllPrims();
			ScanAllAvatars();
		}

		public void StopPlugin(RadegastInstance inst)
		{
			instance.Client.Objects.ObjectUpdate -= Objects_ObjectUpdate;
			instance.Client.Objects.ObjectProperties -= Objects_ObjectProperties;
			instance.Client.Self.GroupChatJoined -= Self_GroupChatJoined;

			requestedUpdates.Clear();
		}

		/*private void clearPreviousRequestsTimerCallback(object state)
		{
			Output("Tick");

			List<UUID> timedOutEntries = new List<UUID>();

			foreach (var request in previousRequests)
			{
				if (DateTime.Now > request.Value.AddSeconds(60))
				{
					timedOutEntries.Add(request.Key);
				}
			}

			foreach (var item in timedOutEntries)
			{
				previousRequests.Remove(item);
			}
		}*/

		private void SendGroupMessage(string message)
		{
			foreach (var group in groupsToMessage)
			{
				if (!instance.Client.Self.GroupChatSessions.ContainsKey(group))
				{
					instance.Client.Self.RequestJoinGroupChat(group);
					if (!WaitForSessionStart.WaitOne(2000))
					{
						Output("Timed out joining group secondlife:///app/group/" + group + "/about");
						continue;
					}
				}

				instance.Client.Self.InstantMessageGroup(group, message);
			}
		}

		private void SendPrivateMessage(string message)
		{
			foreach (var privateMessageTarget in privateMessageTargets)
			{
				var friend = instance.Client.Friends.FriendList.Find(n => n.IsOnline && n.UUID == privateMessageTarget);
				if (friend != null)
				{
					instance.Client.Self.InstantMessage(privateMessageTarget, message);
				}
			}
		}

		private void ScanAllAvatars()
		{
			foreach (var simulator in instance.Client.Network.Simulators)
			{
				if (!simulator.Connected)
				{
					continue;
				}

				List<Avatar> avis = simulator.ObjectsAvatars.FindAll((Avatar a) => true);
				foreach (Avatar avatar in avis)
				{
					simulator.ObjectsPrimitives
						.FindAll((Primitive child) => child.ParentID == avatar.LocalID)
						.ForEach((Primitive attachedPrim) =>
						{
							CheckUpdatedObject(simulator, attachedPrim);
							simulator.ObjectsPrimitives
								.FindAll((Primitive child) => child.ParentID == attachedPrim.LocalID)
								.ForEach(prim => CheckUpdatedObject(simulator, prim));
						});
				}
			}
		}

		private void ScanAllPrims()
		{
			foreach (var simulator in instance.Client.Network.Simulators)
			{
				if (!simulator.Connected)
				{
					continue;
				}

				List<Primitive> mainPrims = simulator.ObjectsPrimitives.FindAll((Primitive root) => root.ParentID == 0);
				foreach (Primitive mainPrim in mainPrims)
				{
					CheckUpdatedObject(simulator, mainPrim);
					simulator.ObjectsPrimitives
							.FindAll((Primitive child) => child.ParentID == mainPrim.LocalID)
							.ForEach(prim => CheckUpdatedObject(simulator, prim));
				}
			}
		}

		/// <summary>
		/// Sends a request to get the deailed properties of a specified prim.
		/// </summary>
		/// <param name="sim">Sim that contains this prim.</param>
		/// <param name="prim">Prim to get the properties of.</param>
		private void RequestObjectDetails(Simulator sim, Primitive prim)
		{
			lock (requestedUpdates)
			{
				if (!requestedUpdates.ContainsKey(prim.ID))
				{
					requestedUpdates.Add(prim.ID, prim.LocalID);
				}
			}

			instance.Client.Objects.SelectObject(sim, prim.LocalID);
		}

		/// <summary>
		/// Retrieves the top-most parent prim for the passed in prim.
		/// </summary>
		/// <param name="sim">Sim that contains this prim.</param>
		/// <param name="prim">The prim to get the top-most parent of.</param>
		/// <returns>Top-most parent prim (or this prim if there is no parent or other error occurs)</returns>
		private Primitive GetRootParent(Simulator sim, Primitive prim)
		{
			Primitive primIter = prim;

			for (int i = 0; i < 100; i++)
			{
				if (primIter.ParentID == primIter.LocalID)
					return primIter;

				if (primIter.ParentID == 0)
					return primIter;

				if (!sim.ObjectsPrimitives.ContainsKey(primIter.ParentID))
					return primIter;

				primIter = sim.ObjectsPrimitives[primIter.ParentID];
			}

			return primIter;
		}

		/// <summary>
		/// Handles logic for dealing with large prims. All prims that are sent to this function have already been
		/// filtered so these are all the untrusted prims.
		/// </summary>
		/// <param name="sim">Sim that contains this prim.</param>
		/// <param name="prim">The top most parent of the large prim.</param>
		/// <param name="size">Size of the large prim.</param>
		private void HandleLargeObject(Simulator sim, Primitive prim, float size)
		{
			string message = "";

			if (prim.IsAttachment)
			{
				Vector3 position = sim.AvatarPositions.ContainsKey(prim.Properties.OwnerID) ? sim.AvatarPositions[prim.Properties.OwnerID] : Vector3.Zero;

				message = string.Format("\n** Large attached object **:\n    Key: {0}\n    Name: {1}\n    Owner: secondlife:///app/agent/{2}/about\n    Sim: secondlife://{3}/{4}/{5}/{6}\n    Size: {7}",
					prim.ID,
					prim.Properties.Name,
					prim.Properties.OwnerID,
					sim.Name.Replace(" ", "%20"),
					(int)position.X,
					(int)position.Y,
					(int)position.Z,
					size);
			}
			else
			{
				instance.Names.Get(prim.Properties.OwnerID, true);
				string legacyName = instance.Names.GetLegacyName(prim.Properties.OwnerID);
				if (legacyName == RadegastInstance.INCOMPLETE_NAME)
				{
					legacyName = prim.Properties.OwnerID.ToString();
				}

				if (ignoredOwners.Contains(prim.Properties.OwnerID))
				{
					Output("Ignored owner: secondlife:///app/agent/" + prim.Properties.OwnerID + "/about");
					Output("new UUID(\"" + prim.ID + "\"), // Owner: " + legacyName + " | Name: " + prim.Properties.Name);
					return;
				}

				Vector3 position = prim.Position;
				message = string.Format("\nLarge prim props:\n    Key: {0}\n    Name: {1}\n    Owner: secondlife:///app/agent/{2}/about\n    Sim: secondlife://{3}/{4}/{5}/{6}\n    Size: {7}\n    new UUID(\"{8}\"), // Owner: {9} | Name: {10}",
					prim.ID,
					prim.Properties.Name,
					prim.Properties.OwnerID,
					sim.Name.Replace(" ", "%20"),
					(int)position.X,
					(int)position.Y,
					(int)position.Z,
					size,
					prim.ID,
					legacyName,
					prim.Properties.Name);
			}

			Output(message);
			//SendPrivateMessage(message);
			//SendGroupMessage(message);

			Thread.Sleep(1000);
		}

		/// <summary>
		/// Checks the specified prim to see if it's considered large.
		/// </summary>
		/// <param name="sim">Sim the prim exists in.</param>
		/// <param name="prim">Prim to check size of. If no properties, then we will request the properties for this object</param>
		private void CheckUpdatedObject(Simulator sim, Primitive prim)
		{
			float size = prim.Scale.Length();
			if (size < 50)
			{
				return;
			}

			if (!prim.IsAttachment && !regionsToCheckForObjects.Contains(sim.Name.ToLower()))
			{
				return;
			}

			Primitive parentPrim = GetRootParent(sim, prim);
			if (ignoredObjects.Contains(parentPrim.ID))
			{
				return;
			}

			if (previousRequests.ContainsKey(parentPrim.ID))
			{
				if (DateTime.Now < previousRequests[parentPrim.ID].AddSeconds(5))
				{
					return;
				}

				previousRequests[parentPrim.ID] = DateTime.Now;
			}
			else
			{
				previousRequests.Add(parentPrim.ID, DateTime.Now);
			}

			if (parentPrim.Properties != null)
			{
				HandleLargeObject(sim, parentPrim, size);
			}

			if (!parentPrim.IsAttachment)
			{
				//Output(string.Format("Large object:  Key: {0} | Owner: looking up... | Sim: secondlife://{1}/{2}/{3}/{4} | Size: {5}", parentPrim.ID, sim.Name.Replace(" ", "%20"), parentPrim.Position.X, parentPrim.Position.Y, parentPrim.Position.Z, size));
				RequestObjectDetails(sim, parentPrim);
			}
			else
			{
				Output(string.Format("Large attached object:  Key: {0} | Owner: looking up... | Sim: secondlife://{1}/0/0/0 | Size: {2}", 
					parentPrim.ID, 
					sim.Name.Replace(" ", "%20"), 
					size));

				RequestObjectDetails(sim, parentPrim);
			}
		}

		/// <summary>
		/// Outputs message to local client.
		/// </summary>
		/// <param name="message">Message to output.</param>
		private void Output(string message)
		{
			instance.TabConsole.DisplayNotificationInChat(message, ChatBufferTextStyle.StatusBlue);
		}

		void Objects_ObjectUpdate(object sender, OpenMetaverse.PrimEventArgs e)
		{
			CheckUpdatedObject(e.Simulator, e.Prim);
		}

		void Objects_ObjectProperties(object sender, ObjectPropertiesEventArgs e)
		{
			uint localObjectId;

			lock (requestedUpdates)
			{
				if (!requestedUpdates.ContainsKey(e.Properties.ObjectID))
				{
					return;
				}

				localObjectId = requestedUpdates[e.Properties.ObjectID];
				requestedUpdates.Remove(e.Properties.ObjectID);
			}

			if (!e.Simulator.ObjectsPrimitives.ContainsKey(localObjectId))
			{
				return;
			}

			Primitive prim = e.Simulator.ObjectsPrimitives[localObjectId];
			if (prim == null)
			{
				Output("prim == null: " + e.Properties.ObjectID);
				return;
			}

			HandleLargeObject(e.Simulator, prim, prim.Scale.Length());
		}

		void Self_GroupChatJoined(object sender, GroupChatJoinedEventArgs e)
		{
			if (e.Success)
			{
				Output("Joined group chat for group: secondlife:///app/group/" + e.SessionID + "/about");
			}
			else
			{
				Output("Failed to join group chat for group: secondlife:///app/group/" + e.SessionID + "/about");
			}

			WaitForSessionStart.Set();
		}

	}
}
