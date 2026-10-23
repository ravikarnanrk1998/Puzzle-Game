import 'dart:async';

import 'package:flutter/material.dart';

import '../game/bike_art.dart';
import '../game/bike_game.dart';
import '../shop/catalog.dart';
import '../shop/player_progress.dart';
import '../shop/store_service.dart';
import 'overlays.dart' show DiamondChip, WalletChip;

const _bg = Color(0xFF0D1020);
const _card = Color(0xFF171C2B);
const _gold = Color(0xFFFFC107);
const _green = Color(0xFF00C853);

class ShopScreen extends StatefulWidget {
  const ShopScreen({
    super.key,
    required this.progress,
    required this.store,
    this.initialTab = 0,
  });

  final PlayerProgress progress;
  final StoreService store;

  /// The tab it opens on: one of the tabs below.
  final int initialTab;

  static const int bikesTab = 0;
  static const int carsTab = 1;
  static const int skinsTab = 2;
  static const int coinsTab = 3;
  static const int diamondsTab = 4;

  @override
  State<ShopScreen> createState() => _ShopScreenState();
}

class _ShopScreenState extends State<ShopScreen> {
  StreamSubscription<String>? _sub;

  PlayerProgress get _progress => widget.progress;
  StoreService get _store => widget.store;

  @override
  void initState() {
    super.initState();
    _sub = _store.messages.listen(_toast);
    if (_store.enabled && _store.products.isEmpty) _store.init();
  }

  @override
  void dispose() {
    _sub?.cancel();
    super.dispose();
  }

  void _toast(String message) {
    if (!mounted) return;
    ScaffoldMessenger.of(context)
      ..hideCurrentSnackBar()
      ..showSnackBar(SnackBar(
        content: Text(message),
        behavior: SnackBarBehavior.floating,
      ));
  }

  @override
  Widget build(BuildContext context) {
    return DefaultTabController(
      length: 5,
      initialIndex: widget.initialTab,
      child: ListenableBuilder(
        listenable: Listenable.merge([_progress, _store]),
        builder: (context, _) => Scaffold(
          backgroundColor: _bg,
          appBar: AppBar(
            backgroundColor: const Color(0xFF141824),
            foregroundColor: Colors.white,
            title: const Text(
              'GARAGE',
              style: TextStyle(
                fontWeight: FontWeight.w900,
                fontStyle: FontStyle.italic,
              ),
            ),
            actions: [
              Padding(
                padding: const EdgeInsets.only(right: 12),
                child: Center(
                  child: WalletChip(
                    coins: _progress.wallet,
                    doubled: _progress.doubleCoins,
                  ),
                ),
              ),
            ],
            // Five tabs don't fit a narrow phone at full width, so the row
            // scrolls there.
            bottom: const TabBar(
              isScrollable: true,
              tabAlignment: TabAlignment.center,
              labelPadding: EdgeInsets.symmetric(horizontal: 12),
              indicatorColor: Color(0xFFFF9800),
              labelColor: Colors.white,
              unselectedLabelColor: Colors.white54,
              labelStyle: TextStyle(fontWeight: FontWeight.w900),
              tabs: [
                Tab(icon: Icon(Icons.two_wheeler), text: 'BIKES'),
                Tab(icon: Icon(Icons.directions_car), text: 'CARS'),
                Tab(icon: Icon(Icons.format_paint), text: 'SKINS'),
                Tab(icon: Icon(Icons.monetization_on), text: 'COINS'),
                Tab(icon: Icon(Icons.diamond), text: 'DIAMONDS'),
              ],
            ),
          ),
          body: TabBarView(
            children: [
              _ridesTab(Catalog.bikes),
              _ridesTab(Catalog.cars),
              _skinsTab(),
              _coinsTab(),
              _diamondsTab(),
            ],
          ),
        ),
      ),
    );
  }

  // ---------------------------------------------------------- bikes, cars

  /// [rides] wearing the skin picked in the skins tab.
  Widget _ridesTab(List<Ride> rides) {
    return ListView.separated(
      padding: const EdgeInsets.all(16),
      itemCount: rides.length,
      separatorBuilder: (_, _) => const SizedBox(height: 12),
      itemBuilder: (context, i) {
        final ride = rides[i];
        final selected = _progress.selected == ride.id;
        return _Card(
          highlight: selected ? ride.labelColor : null,
          leading: _RidePreview(ride, _progress.selectedSkin),
          title: ride.name,
          badge: ride.isPremium ? 'PREMIUM' : null,
          subtitle: ride.perk ?? 'Classic racer',
          action: _rideAction(ride),
        );
      },
    );
  }

  Widget _rideAction(Ride ride) {
    if (_progress.selected == ride.id) {
      return const _Tag(label: 'RIDING', color: _green);
    }
    if (_progress.owns(ride)) {
      return FilledButton(
        onPressed: () => _progress.select(ride),
        child: const Text('RIDE'),
      );
    }
    if (ride.isPremium) return _priceButton(ride.productId!);
    return _coinButton(ride.coinPrice,
        bought: ride.name, buy: () => _progress.buyWithCoins(ride));
  }

  // ---------------------------------------------------------------- skins

  /// Every skin, shown on the bike or car being ridden.
  Widget _skinsTab() {
    final ride = _progress.selectedRide;
    return ListView(
      padding: const EdgeInsets.all(16),
      children: [
        Padding(
          padding: const EdgeInsets.only(bottom: 12),
          child: Text(
            'A skin goes on whichever bike or car you ride. '
            'Shown here on your ${ride.name}.',
            textAlign: TextAlign.center,
            style: const TextStyle(color: Colors.white38, fontSize: 12),
          ),
        ),
        for (final skin in Catalog.skins) ...[
          _Card(
            highlight: _progress.skinId == skin.id ? _green : null,
            leading: _RidePreview(ride, skin),
            title: skin.name,
            badge: skin.isPremium ? 'PREMIUM' : null,
            subtitle: skin.description,
            action: _skinAction(skin),
          ),
          const SizedBox(height: 12),
        ],
      ],
    );
  }

  Widget _skinAction(Skin skin) {
    if (_progress.skinId == skin.id) {
      return const _Tag(label: 'EQUIPPED', color: _green);
    }
    if (_progress.hasSkin(skin)) {
      return FilledButton(
        onPressed: () => _progress.selectSkin(skin),
        child: const Text('USE'),
      );
    }
    if (skin.isPremium) return _priceButton(skin.productId!);
    return _coinButton(skin.coinPrice,
        bought: '${skin.name} skin', buy: () => _progress.buySkin(skin));
  }

  /// Buys something for [price] coins with [buy], which returns false when
  /// the wallet is short. [bought] names it in the message after.
  Widget _coinButton(int price,
      {required String bought, required bool Function() buy}) {
    final affordable = _progress.wallet >= price;
    return FilledButton.icon(
      onPressed: () {
        if (buy()) {
          _toast('$bought unlocked!');
        } else {
          _toast('Not enough coins. Keep riding or grab a coin pack!');
        }
      },
      style: FilledButton.styleFrom(
        backgroundColor:
            affordable ? const Color(0xFFFF9800) : Colors.white24,
        foregroundColor: Colors.white,
      ),
      icon: const Icon(Icons.monetization_on, color: _gold, size: 18),
      label: Text(formatNumber(price)),
    );
  }

  // ---------------------------------------------------------------- coins

  /// A note at the top of a tab that sells for real money, when the store
  /// can't be used.
  List<Widget> _storeStatus() {
    return [
      if (!_store.enabled)
        const _Info(
            'Real-money purchases work in the Android app installed '
            'from Google Play.')
      else if (_store.loading && _store.products.isEmpty)
        const Padding(
          padding: EdgeInsets.all(24),
          child: Center(child: CircularProgressIndicator()),
        )
      else if (!_store.available || _store.products.isEmpty)
        _Info(
          'Google Play store is not available right now. '
          'Check your internet connection.',
          onRetry: _store.init,
        ),
    ];
  }

  Widget _coinsTab() {
    return ListView(
      padding: const EdgeInsets.all(16),
      children: [
        ..._storeStatus(),
        for (var i = 0; i < Catalog.coinPacks.length; i++) ...[
          _Card(
            leading: _CoinStack(count: i + 1),
            title: '${formatNumber(Catalog.coinPacks[i].coins)} COINS',
            badge: Catalog.coinPacks[i].badge,
            subtitle: 'Unlock rides and skins, continue after crashes',
            action: _priceButton(Catalog.coinPacks[i].productId),
          ),
          const SizedBox(height: 12),
        ],
        _Card(
          highlight: _progress.doubleCoins ? _green : null,
          leading: Container(
            width: 64,
            height: 64,
            alignment: Alignment.center,
            decoration: const BoxDecoration(
              shape: BoxShape.circle,
              gradient: LinearGradient(
                colors: [Color(0xFF00E676), Color(0xFF00897B)],
              ),
            ),
            child: const Text(
              'x2',
              style: TextStyle(
                color: Colors.white,
                fontSize: 26,
                fontWeight: FontWeight.w900,
                fontStyle: FontStyle.italic,
              ),
            ),
          ),
          title: 'DOUBLE COINS',
          badge: 'FOREVER',
          subtitle: 'Every coin you collect counts twice',
          action: _progress.doubleCoins
              ? const _Tag(label: 'OWNED', color: _green)
              : _priceButton(Catalog.doubleCoinsId),
        ),
        const SizedBox(height: 16),
        if (_store.enabled)
          Center(
            child: TextButton.icon(
              onPressed: _store.restore,
              icon: const Icon(Icons.restore_rounded),
              label: const Text('Restore purchases'),
              style: TextButton.styleFrom(foregroundColor: Colors.white70),
            ),
          ),
        const SizedBox(height: 8),
        const Text(
          'Coins you collect while riding are added to your wallet for free.',
          textAlign: TextAlign.center,
          style: TextStyle(color: Colors.white38, fontSize: 12),
        ),
      ],
    );
  }

  // ------------------------------------------------------------- diamonds

  Widget _diamondsTab() {
    return ListView(
      padding: const EdgeInsets.all(16),
      children: [
        ..._storeStatus(),
        Padding(
          padding: const EdgeInsets.only(bottom: 12),
          child: Center(child: DiamondChip(count: _progress.diamonds)),
        ),
        for (final pack in Catalog.diamondPacks) ...[
          _Card(
            leading: Container(
              width: 64,
              height: 64,
              alignment: Alignment.center,
              decoration: const BoxDecoration(
                shape: BoxShape.circle,
                gradient: LinearGradient(
                  colors: [Color(0xFF40C4FF), Color(0xFF304FFE)],
                ),
              ),
              child: const Icon(Icons.diamond, color: Colors.white, size: 34),
            ),
            title: pack.diamonds == 1
                ? '1 DIAMOND'
                : '${pack.diamonds} DIAMONDS',
            badge: pack.badge,
            subtitle: pack.diamonds == 1
                ? 'Create one 1 VS 1 match'
                : 'Create ${pack.diamonds} 1 VS 1 matches',
            action: _priceButton(pack.productId),
          ),
          const SizedBox(height: 12),
        ],
        const SizedBox(height: 4),
        const Text(
          'Creating a 1 VS 1 code costs '
          '${Catalog.versusCost} diamond. Joining a friend is free.\n'
          'Free diamonds: ${PlayerProgress.dailyDiamonds} every day on the '
          'menu, and 1 for every ${BikeGame.levelsPerDiamond} levels you '
          'complete in a run (not in 1 VS 1).',
          textAlign: TextAlign.center,
          style: TextStyle(color: Colors.white38, fontSize: 12),
        ),
      ],
    );
  }

  Widget _priceButton(String productId) {
    if (_store.isBusy(productId)) {
      return const SizedBox(
        width: 28,
        height: 28,
        child: CircularProgressIndicator(strokeWidth: 3),
      );
    }
    final product = _store.product(productId);
    return FilledButton(
      onPressed: () => _store.buy(productId),
      style: FilledButton.styleFrom(
        backgroundColor: product == null ? Colors.white24 : _green,
        foregroundColor: Colors.white,
      ),
      child: Text(product?.price ?? 'BUY'),
    );
  }
}

class _Card extends StatelessWidget {
  const _Card({
    required this.leading,
    required this.title,
    required this.subtitle,
    required this.action,
    this.badge,
    this.highlight,
  });

  final Widget leading;
  final String title;
  final String subtitle;
  final Widget action;
  final String? badge;
  final Color? highlight;

  @override
  Widget build(BuildContext context) {
    return Container(
      padding: const EdgeInsets.all(12),
      decoration: BoxDecoration(
        color: _card,
        borderRadius: BorderRadius.circular(20),
        border: Border.all(
          color: highlight ?? Colors.white12,
          width: highlight == null ? 1 : 2,
        ),
      ),
      child: Row(
        children: [
          leading,
          const SizedBox(width: 14),
          Expanded(
            child: Column(
              crossAxisAlignment: CrossAxisAlignment.start,
              children: [
                if (badge != null)
                  Padding(
                    padding: const EdgeInsets.only(bottom: 4),
                    child: _Tag(label: badge!, color: _gold, small: true),
                  ),
                Text(
                  title,
                  style: const TextStyle(
                    color: Colors.white,
                    fontSize: 17,
                    fontWeight: FontWeight.w900,
                  ),
                ),
                const SizedBox(height: 2),
                Text(
                  subtitle,
                  style: const TextStyle(color: Colors.white60, fontSize: 13),
                ),
              ],
            ),
          ),
          const SizedBox(width: 8),
          action,
        ],
      ),
    );
  }
}

class _Tag extends StatelessWidget {
  const _Tag({required this.label, required this.color, this.small = false});

  final String label;
  final Color color;
  final bool small;

  @override
  Widget build(BuildContext context) {
    return Container(
      padding: EdgeInsets.symmetric(
          horizontal: small ? 8 : 12, vertical: small ? 2 : 8),
      decoration: BoxDecoration(
        color: color.withValues(alpha: 0.18),
        borderRadius: BorderRadius.circular(20),
        border: Border.all(color: color),
      ),
      child: Text(
        label,
        style: TextStyle(
          color: color,
          fontSize: small ? 10 : 13,
          fontWeight: FontWeight.w900,
          letterSpacing: 1,
        ),
      ),
    );
  }
}

class _Info extends StatelessWidget {
  const _Info(this.text, {this.onRetry});

  final String text;
  final VoidCallback? onRetry;

  @override
  Widget build(BuildContext context) {
    return Container(
      margin: const EdgeInsets.only(bottom: 12),
      padding: const EdgeInsets.all(14),
      decoration: BoxDecoration(
        color: const Color(0x3342A5F5),
        borderRadius: BorderRadius.circular(16),
        border: Border.all(color: const Color(0x6642A5F5)),
      ),
      child: Row(
        children: [
          const Icon(Icons.info_outline_rounded, color: Color(0xFF90CAF9)),
          const SizedBox(width: 10),
          Expanded(
            child: Text(text, style: const TextStyle(color: Colors.white70)),
          ),
          if (onRetry != null)
            TextButton(onPressed: onRetry, child: const Text('RETRY')),
        ],
      ),
    );
  }
}

class _CoinStack extends StatelessWidget {
  const _CoinStack({required this.count});

  final int count;

  @override
  Widget build(BuildContext context) {
    return SizedBox(
      width: 64,
      height: 64,
      child: Stack(
        alignment: Alignment.center,
        children: [
          for (var i = 0; i < count; i++)
            Positioned(
              left: 8 + i * 8.0,
              top: 18 - i * 6.0,
              child: const Icon(Icons.monetization_on,
                  color: _gold, size: 34),
            ),
        ],
      ),
    );
  }
}

/// A bike or car on a strip of road, wearing [skin].
class _RidePreview extends StatelessWidget {
  const _RidePreview(this.ride, this.skin);

  final Ride ride;
  final Skin skin;

  @override
  Widget build(BuildContext context) {
    return Container(
      width: 64,
      height: 92,
      // Keeps a premium ride's glow inside the box.
      clipBehavior: Clip.antiAlias,
      decoration: BoxDecoration(
        color: const Color(0xFF3D4049),
        borderRadius: BorderRadius.circular(14),
      ),
      child: CustomPaint(painter: _RidePreviewPainter(ride, skin)),
    );
  }
}

class _RidePreviewPainter extends CustomPainter {
  _RidePreviewPainter(this.ride, this.skin);

  final Ride ride;
  final Skin skin;

  @override
  void paint(Canvas canvas, Size size) {
    final dash = Paint()..color = const Color(0x55FFFFFF);
    for (var y = 6.0; y < size.height; y += 22) {
      canvas.drawRect(Rect.fromLTWH(6, y, 3, 11), dash);
      canvas.drawRect(Rect.fromLTWH(size.width - 9, y, 3, 11), dash);
    }
    canvas.save();
    canvas.translate(size.width / 2, size.height / 2);
    canvas.scale(size.height / 80);
    paintRide(canvas, ride, skin: skin);
    canvas.restore();
  }

  @override
  bool shouldRepaint(_RidePreviewPainter old) =>
      old.ride != ride || old.skin != skin;
}
