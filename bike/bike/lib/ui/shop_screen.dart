import 'dart:async';

import 'package:flutter/material.dart';

import '../game/bike_art.dart';
import '../shop/catalog.dart';
import '../shop/player_progress.dart';
import '../shop/store_service.dart';
import 'overlays.dart' show WalletChip;

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

  /// 0 = bikes, 1 = coins.
  final int initialTab;

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
      length: 2,
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
            bottom: const TabBar(
              indicatorColor: Color(0xFFFF9800),
              labelColor: Colors.white,
              unselectedLabelColor: Colors.white54,
              labelStyle: TextStyle(fontWeight: FontWeight.w900),
              tabs: [
                Tab(icon: Icon(Icons.two_wheeler), text: 'BIKES'),
                Tab(icon: Icon(Icons.monetization_on), text: 'COINS'),
              ],
            ),
          ),
          body: TabBarView(children: [_bikesTab(), _coinsTab()]),
        ),
      ),
    );
  }

  // ---------------------------------------------------------------- bikes

  Widget _bikesTab() {
    return ListView.separated(
      padding: const EdgeInsets.all(16),
      itemCount: Catalog.bikes.length,
      separatorBuilder: (_, _) => const SizedBox(height: 12),
      itemBuilder: (context, i) {
        final bike = Catalog.bikes[i];
        final selected = _progress.selected == bike.id;
        return _Card(
          highlight: selected ? bike.color : null,
          leading: Container(
            width: 64,
            height: 92,
            decoration: BoxDecoration(
              color: const Color(0xFF3D4049),
              borderRadius: BorderRadius.circular(14),
            ),
            child: CustomPaint(painter: _BikePreviewPainter(bike)),
          ),
          title: bike.name,
          badge: bike.isPremium ? 'PREMIUM' : null,
          subtitle: bike.perk ?? 'Classic racer',
          action: _bikeAction(bike),
        );
      },
    );
  }

  Widget _bikeAction(BikeSkin bike) {
    if (_progress.selected == bike.id) {
      return const _Tag(label: 'RIDING', color: _green);
    }
    if (_progress.owns(bike)) {
      return FilledButton(
        onPressed: () => _progress.select(bike),
        child: const Text('RIDE'),
      );
    }
    if (bike.isPremium) return _priceButton(bike.productId!);
    final affordable = _progress.wallet >= bike.coinPrice;
    return FilledButton.icon(
      onPressed: () {
        if (_progress.buyWithCoins(bike)) {
          _toast('${bike.name} unlocked!');
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
      label: Text(formatNumber(bike.coinPrice)),
    );
  }

  // ---------------------------------------------------------------- coins

  Widget _coinsTab() {
    return ListView(
      padding: const EdgeInsets.all(16),
      children: [
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
        for (var i = 0; i < Catalog.coinPacks.length; i++) ...[
          _Card(
            leading: _CoinStack(count: i + 1),
            title: '${formatNumber(Catalog.coinPacks[i].coins)} COINS',
            badge: Catalog.coinPacks[i].badge,
            subtitle: 'Unlock bikes and continue after crashes',
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

class _BikePreviewPainter extends CustomPainter {
  _BikePreviewPainter(this.bike);

  final BikeSkin bike;

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
    paintBike(canvas, bike);
    canvas.restore();
  }

  @override
  bool shouldRepaint(_BikePreviewPainter old) => old.bike != bike;
}
